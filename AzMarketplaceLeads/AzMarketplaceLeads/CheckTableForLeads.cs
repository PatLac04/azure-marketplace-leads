using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace AzMarketplaceLeads
{
    // https://docs.microsoft.com/en-us/azure/azure-functions/functions-triggers-bindings#register-binding-extensions
    // https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-table
    // https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-sendgrid
    public static class CheckTableForLeads
    {
        private static readonly string sendGridApiKey = Environment.GetEnvironmentVariable("SendGridApiKey");
        private static readonly string sentFromEmail = Environment.GetEnvironmentVariable("SentFromEmail");
        private static readonly string sendToEmail = Environment.GetEnvironmentVariable("SendToEmail");

        [FunctionName("CheckTableForLeads")]
        public static async Task RunAsync([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer,
            [Table("MarketplaceLeads", Connection = "StorageForLeads")] CloudTable marketplaceLeadsTable,
            [Table("LastRunDatetime", Connection = "StorageForLastExecDatetime")] CloudTable lastRunDatetimeTable,
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var lastExecutionDatetime = await GetLastRunDatetime(lastRunDatetimeTable, log);

            // I believe the Partition Key correspond to the date the lead was created.
            // In our case, we don't really care for the PartitionKey
            TableQuery<MarketplaceLeadEntity> rangeQuery = new TableQuery<MarketplaceLeadEntity>().Where(
                TableQuery.GenerateFilterConditionForDate("Timestamp", QueryComparisons.GreaterThan, lastExecutionDatetime));

            // Execute the query and loop through the results
            foreach (MarketplaceLeadEntity entity in
                await marketplaceLeadsTable.ExecuteQuerySegmentedAsync(rangeQuery, null))
            {
                var result = await SendEmailAsync(entity, log);
                if (result.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    log.LogInformation("Email Sent");
                }
                else
                {
                    log.LogError($"Error sending email, SendGrid StatusCode: {result.StatusCode}");
                }
            }

            await SetLastRunDatetime(lastRunDatetimeTable, log);
        }

        private static async Task<DateTime> GetLastRunDatetime(CloudTable lastRunDatetimeTable, ILogger log)
        {
            DateTime lastRan = DateTime.UtcNow.AddMonths(-1);

            // When was the last time job was executed
            TableOperation retrieveOperation = TableOperation.Retrieve<LastExecutionEntity>("CheckTableForLeads", "1");
            TableResult retrievedResult = await lastRunDatetimeTable.ExecuteAsync(retrieveOperation);

            if (retrievedResult.Result != null)
            {
                if (DateTime.TryParseExact(((LastExecutionEntity)retrievedResult.Result).LastExecutionDatetime,
                    "yyyy-MM-ddTHH:mm:ss.fffZ",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out lastRan))
                {
                    log.LogInformation($"CheckTableForLeads was last executed on {lastRan.ToString()}");
                }
            }

            return lastRan;
        }

        private static async Task SetLastRunDatetime(CloudTable lastRunDatetimeTable, ILogger log)
        {
            LastExecutionEntity lastRunEntity = new LastExecutionEntity("CheckTableForLeads")
            {
                LastExecutionDatetime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
            };

            TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(lastRunEntity);
            await lastRunDatetimeTable.ExecuteAsync(insertOrReplaceOperation);
        }

        private static async Task<Response> SendEmailAsync(MarketplaceLeadEntity lead, ILogger log)
        {
            var sendGridclient = new SendGridClient(sendGridApiKey);
            var email = new SendGridMessage();
            CustomerInfo json = JsonConvert.DeserializeObject<CustomerInfo>(lead.CustomerInfo);

            var sb = new StringBuilder();
            sb.AppendLine($"<h3>A new Lead has been created for {lead.OfferDisplayName}!</h3><br>");
            sb.AppendLine($"<table><tr><td><b>FirstName:</b></td><td>{json.FirstName}</td></tr>");
            sb.AppendLine($"<tr><td><b>LastName:</b></td><td>{json.LastName}</td></tr>");
            sb.AppendLine($"<tr><td><b>Title:</b></td><td>{json.Title}</td></tr>");
            sb.AppendLine($"<tr><td><b>Company:</b></td><td>{json.Company}</td></tr>");
            sb.AppendLine($"<tr><td><b>Email:</b></td><td>{json.Email}</td></tr>");
            sb.AppendLine($"<tr><td><b>Phone:</b></td><td>{json.Phone}</td></tr>");
            sb.AppendLine($"<tr><td><b>Country:</b></td><td>{json.Country}</td></tr></table>");

            email.SetFrom(new EmailAddress(sentFromEmail));
            email.SetSubject($"Azure Marketplace lead for {lead.OfferDisplayName}");
            email.AddContent(MimeType.Html, sb.ToString());
            email.AddTo(new EmailAddress(sendToEmail));

            Response response = await sendGridclient.SendEmailAsync(email);

            log.LogInformation(email.Serialize());

            return response;
        }
    }
}

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AzMarketplaceLeads
{
    public class MarketplaceLead : TableEntity
    {
        public string ActionCode { get; set; }
        public string CreatedTime { get; set; }
        public string CustomerInfo { get; set; }
        public string Description { get; set; }
        public string LeadSource { get; set; }
        public string OfferDisplayName { get; set; }
    }

    // https://docs.microsoft.com/en-us/azure/azure-functions/functions-triggers-bindings#register-binding-extensions
    // https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-table
    // https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-sendgrid
    public static class CheckTableForLeads
    {
        private static readonly string sendGridApiKey = Environment.GetEnvironmentVariable("SendGridApiKey");

        [FunctionName("CheckTableForLeads")]
        public static async Task RunAsync([TimerTrigger("0 */2 * * * *")]TimerInfo myTimer,
            [Table("MarketplaceLeads", Connection = "StorageForLeads")] CloudTable cloudTable,
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            TableQuery<MarketplaceLead> query = new TableQuery<MarketplaceLead>().Where(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "9:2F28:2F2018"));

            // Execute the query and loop through the results
            foreach (MarketplaceLead entity in
                await cloudTable.ExecuteQuerySegmentedAsync(query, null))
            {
                //log.LogInformation($"PartitionKey: {entity.PartitionKey}");
                //log.LogInformation($"RowKey: {entity.RowKey}");
                //log.LogInformation($"Timestamp: {entity.Timestamp}");
                //log.LogInformation($"ActionCode: {entity.ActionCode}");
                //log.LogInformation($"CreatedTime: {entity.CreatedTime}");
                //log.LogInformation($"CustomerInfo: {entity.CustomerInfo}");
                //log.LogInformation($"Description: {entity.Description}");
                //log.LogInformation($"LeadSource: {entity.LeadSource}");
                //log.LogInformation($"OfferDisplayName: {entity.OfferDisplayName}");

                var result = await SendEmailAsync(entity, log);
                log.LogInformation($"Response from SendGrid: {result.StatusCode}");
            }
        }

        private static async Task<Response> SendEmailAsync(MarketplaceLead lead, ILogger log)
        {
            var sendGridclient = new SendGridClient(sendGridApiKey);
            var email = new SendGridMessage();
            CustomerInfo json = JsonConvert.DeserializeObject<CustomerInfo>(lead.CustomerInfo);

            var sb = new StringBuilder();
            sb.AppendLine($"<h3>A new Lead has been created for {lead.OfferDisplayName}!</h3>\n\n");
            sb.AppendLine($"FirstName:\t {json.FirstName}\n");
            sb.AppendLine($"LastName:\t {json.LastName}\n");
            sb.AppendLine($"Title:\t {json.Title}\n");
            sb.AppendLine($"Company:\t {json.Company}\n");
            sb.AppendLine($"Email:\t {json.Email}\n");
            sb.AppendLine($"Phone:\t {json.Phone}\n");
            sb.AppendLine($"Country:\t {json.Country}\n");

            email.SetFrom(new EmailAddress("Patrice-Lacroix@hotmail.com", "Patrice Lacroix"));
            email.SetSubject($"Azure Marketplace lead for {lead.OfferDisplayName}");
            email.AddContent(MimeType.Html, sb.ToString());
            email.AddTo(new EmailAddress("Patrice.Lacroix@microsoft.com"));

            Response response = await sendGridclient.SendEmailAsync(email);

            log.LogInformation(email.Serialize());
            log.LogInformation(response.StatusCode.ToString());
            log.LogInformation(response.Headers.ToString());

            return response;
        }
    }
}

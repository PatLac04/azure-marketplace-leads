using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace AzMarketplaceLeads
{
    public class LastExecutionEntity : TableEntity
    {
        public string LastExecutionDatetime { get; set; }

        public LastExecutionEntity(string jobName)
        {
            PartitionKey = jobName;
            RowKey = "1";
        }

        public LastExecutionEntity() { }
    }
}

using Microsoft.WindowsAzure.Storage.Table;

namespace AzMarketplaceLeads
{
    public class MarketplaceLeadEntity : TableEntity
    {
        public string ActionCode { get; set; }
        public string CreatedTime { get; set; }
        public string CustomerInfo { get; set; }
        public string Description { get; set; }
        public string LeadSource { get; set; }
        public string OfferDisplayName { get; set; }
    }
}

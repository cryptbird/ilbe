using System.ComponentModel.DataAnnotations;

namespace DigitalPaymentSystem_ILTC.Models
{
    public class DataLoaderModel
    {
        public class HashRefreshType
        {
            [Required]
            [RegularExpression(@"(?i)(data-master|db-queries-master)$", ErrorMessage = "Invalid hash refresh type. Valid types are: DATA-MASTER and DB-QUERIES-MASTER")]
            public string RefreshType { get; set; }
        }
    }
}

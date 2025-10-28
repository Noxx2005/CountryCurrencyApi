using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CountryCurrencyApi.Models
{
    public class Country
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Capital { get; set; }

        public string? Region { get; set; }

        [Required]
        public long Population { get; set; }

        [Required]
        public string CurrencyCode { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,6)")]
        public decimal? ExchangeRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedGdp { get; set; }

        public string? FlagUrl { get; set; }

        public DateTime LastRefreshedAt { get; set; } = DateTime.UtcNow;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
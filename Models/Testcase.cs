using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace App_thi_tin_hoc.Models
{
    public class Testcase
    {
        [Key]
        public int Id { get; set; }

        public int ProblemId { get; set; }

        [ForeignKey("ProblemId")]
        public Problem? Problem { get; set; }

        public string InputData { get; set; } = string.Empty;

        [Required]
        public string ExpectedOutput { get; set; } = string.Empty;

        public bool IsSample { get; set; } = false; // If true, show as example in problem details
    }
}

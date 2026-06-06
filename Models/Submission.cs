using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace App_thi_tin_hoc.Models
{
    public class Submission
    {
        [Key]
        public int Id { get; set; }

        public int AccountId { get; set; }

        [ForeignKey("AccountId")]
        public Account? Account { get; set; }

        public int ProblemId { get; set; }

        [ForeignKey("ProblemId")]
        public Problem? Problem { get; set; }

        public int? ContestId { get; set; }

        [ForeignKey("ContestId")]
        public Contest? Contest { get; set; }

        [Required]
        public string CodeContent { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Language { get; set; } = "Python"; // Python, Scratch, C++

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending"; // Pending, Judging, Accepted (AC), Wrong Answer (WA), Runtime Error (RTE), Time Limit Exceeded (TLE), Compile Error (CE)

        public int Score { get; set; } = 0; // Out of Problem.Points

        public int ExecutionTimeMs { get; set; } = 0;

        public string Feedback { get; set; } = string.Empty; // Teacher's feedback or error details

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        [StringLength(255)]
        public string ScratchLinkOrFile { get; set; } = string.Empty; // For Scratch submissions

        [StringLength(255)]
        public string? SessionGroup { get; set; }
    }
}

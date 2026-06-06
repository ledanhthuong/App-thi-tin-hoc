using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace App_thi_tin_hoc.Models
{
    public class Contest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(255)]
        public string? CurrentSession { get; set; }

        public bool ShowCodeHints { get; set; } = true;

        // Navigation
        public ICollection<Problem> Problems { get; set; } = new List<Problem>();
        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace App_thi_tin_hoc.Models
{
    public class Problem
    {
        [Key]
        public int Id { get; set; }

        public int ContestId { get; set; }

        [ForeignKey("ContestId")]
        public Contest? Contest { get; set; }

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string StoryDescription { get; set; } = string.Empty; // Kid-friendly description with story details

        public int Points { get; set; } = 100;

        public int TimeLimitMs { get; set; } = 2000; // default 2 seconds

        public int MemoryLimitKb { get; set; } = 65536; // default 64MB

        [Required]
        [StringLength(20)]
        public string Difficulty { get; set; } = "Dễ"; // Dễ, Trung bình, Khó

        [StringLength(255)]
        public string? ImageUrl { get; set; } = string.Empty; // Image for illustration

        // Navigation
        public ICollection<Testcase> Testcases { get; set; } = new List<Testcase>();
        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}

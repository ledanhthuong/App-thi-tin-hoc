using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace App_thi_tin_hoc.Models
{
    public class Account
    {
        [NotMapped]
        public string? PlainTextPassword { get; set; }

        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Student"; // Student, Teacher

        public bool IsSuperAdmin { get; set; } = false;

        public int Points { get; set; } = 0;

        [StringLength(50)]
        public string RankTitle { get; set; } = "Tập sự"; // e.g. "Thợ rèn mật mã", "Dũng sĩ thuật toán"

        [StringLength(255)]
        public string AvatarUrl { get; set; } = "/images/avatars/default.png";

        public int? CreatedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}

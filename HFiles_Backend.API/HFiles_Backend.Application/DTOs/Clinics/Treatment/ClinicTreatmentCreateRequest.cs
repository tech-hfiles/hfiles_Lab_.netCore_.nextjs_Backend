﻿using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Treatment
{
    public class ClinicTreatmentCreateRequest
    {
        [Required]
        public int ClinicId { get; set; }

        [Required]
        [MaxLength(100)]
        public string TreatmentName { get; set; } = null!;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Cost { get; set; }
    }
}

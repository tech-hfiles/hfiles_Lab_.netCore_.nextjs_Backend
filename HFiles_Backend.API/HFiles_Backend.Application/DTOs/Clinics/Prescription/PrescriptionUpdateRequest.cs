﻿using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Prescription
{
    public class PrescriptionUpdateRequest
    {
        [MaxLength(100)]
        public string? MedicationName { get; set; }

        [MaxLength(50)]
        public string? MedicationDosage { get; set; }

        [EnumDataType(typeof(MedicationFrequency))]
        public MedicationFrequency? Frequency { get; set; }

        [EnumDataType(typeof(MedicationTiming))]
        public MedicationTiming? Timing { get; set; }

        [MaxLength(1000)]
        public string? Instructions { get; set; }
    }
}

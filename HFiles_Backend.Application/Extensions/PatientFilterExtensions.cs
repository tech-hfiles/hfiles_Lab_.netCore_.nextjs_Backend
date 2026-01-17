using HFiles_Backend.Application.DTOs.Clinics.Appointment;
using HFiles_Backend.Application.Models.Filters;
using HFiles_Backend.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.Extensions
{
    public static class PatientFilterExtensions
    {
        //public static IEnumerable<PatientDto> ApplyPaymentStatusFilter(
        //    this IEnumerable<PatientDto> patients,
        //    PaymentStatusFilter? paymentStatus)
        //{
        //    if (!paymentStatus.HasValue || paymentStatus.Value == PaymentStatusFilter.All)
        //        return patients;

        //    //return paymentStatus.Value switch
        //    //{
        //    //    PaymentStatusFilter.Paid => patients.Where(p => IsPaymentPaid(p.PaymentMethod)),
        //    //    PaymentStatusFilter.Unpaid => patients.Where(p => IsPaymentUnpaid(p.PaymentMethod)),
        //    //    _ => patients
        //    //};
        //}

        private static bool IsPaymentPaid(PaymentMethod? paymentMethod)
        {
            return paymentMethod.HasValue && paymentMethod.Value != PaymentMethod.Pending;
        }

        private static bool IsPaymentUnpaid(PaymentMethod? paymentMethod)
        {
            return !paymentMethod.HasValue || paymentMethod.Value == PaymentMethod.Pending;
        }
    }
}

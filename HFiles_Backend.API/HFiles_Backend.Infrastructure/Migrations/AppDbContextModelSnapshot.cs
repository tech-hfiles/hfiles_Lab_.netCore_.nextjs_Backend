﻿// <auto-generated />
using System;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("HFiles_Backend.Domain.Entities.Labs.LabAuditLog", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int?>("BranchId")
                        .HasColumnType("int");

                    b.Property<string>("Category")
                        .HasColumnType("longtext");

                    b.Property<string>("Details")
                        .HasColumnType("longtext");

                    b.Property<string>("EntityName")
                        .HasColumnType("longtext");

                    b.Property<string>("HttpMethod")
                        .HasColumnType("longtext");

                    b.Property<string>("IpAddress")
                        .HasColumnType("longtext");

                    b.Property<int?>("LabId")
                        .HasColumnType("int");

                    b.Property<string>("Notifications")
                        .HasColumnType("longtext");

                    b.Property<string>("SessionId")
                        .HasColumnType("longtext");

                    b.Property<long?>("Timestamp")
                        .HasColumnType("bigint");

                    b.Property<string>("Url")
                        .HasColumnType("longtext");

                    b.Property<int?>("UserId")
                        .HasColumnType("int");

                    b.Property<string>("UserRole")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("LabAuditLogs");
                });

            modelBuilder.Entity("HFiles_Backend.Domain.Entities.Labs.LabErrorLog", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Action")
                        .HasColumnType("longtext");

                    b.Property<string>("Category")
                        .HasColumnType("longtext");

                    b.Property<int?>("EntityId")
                        .HasColumnType("int");

                    b.Property<string>("EntityName")
                        .HasColumnType("longtext");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("longtext");

                    b.Property<string>("HttpMethod")
                        .HasColumnType("longtext");

                    b.Property<string>("IpAddress")
                        .HasColumnType("longtext");

                    b.Property<int?>("LabId")
                        .HasColumnType("int");

                    b.Property<string>("SessionId")
                        .HasColumnType("longtext");

                    b.Property<string>("StackTrace")
                        .HasColumnType("longtext");

                    b.Property<long?>("Timestamp")
                        .HasColumnType("bigint");

                    b.Property<string>("Url")
                        .HasColumnType("longtext");

                    b.Property<int?>("UserId")
                        .HasColumnType("int");

                    b.Property<string>("UserRole")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("LabErrorLogs");
                });

            modelBuilder.Entity("HFiles_Backend.Domain.Entities.Labs.LabMember", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("CreatedBy")
                        .HasColumnType("int");

                    b.Property<int>("DeletedBy")
                        .HasColumnType("int");

                    b.Property<long>("EpochTime")
                        .HasColumnType("bigint");

                    b.Property<int>("LabId")
                        .HasColumnType("int");

                    b.Property<string>("PasswordHash")
                        .HasColumnType("longtext");

                    b.Property<int>("PromotedBy")
                        .HasColumnType("int");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("LabMembers");
                });

            modelBuilder.Entity("HFiles_Backend.Domain.Entities.Labs.LabOtpEntry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Email")
                        .HasColumnType("longtext");

                    b.Property<DateTime>("ExpiryTime")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("OtpCode")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("LabOtpEntries");
                });

            modelBuilder.Entity("HFiles_Backend.Domain.Entities.Labs.LabResendReports", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("LabUserReportId")
                        .HasColumnType("int");

                    b.Property<long>("ResendEpochTime")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.ToTable("LabResendReports");
                });

            modelBuilder.Entity("HFiles_Backend.Domain.Entities.Labs.LabSignup", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Address")
                        .HasColumnType("longtext");

                    b.Property<long>("CreatedAtEpoch")
                        .HasColumnType("bigint");

                    b.Property<int>("DeletedBy")
                        .HasColumnType("int");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("HFID")
                        .HasColumnType("varchar(255)");

                    b.Property<bool>("IsSuperAdmin")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("LabName")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("LabReference")
                        .HasColumnType("int");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("PhoneNumber")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("Pincode")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("ProfilePhoto")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("HFID")
                        .IsUnique();

                    b.ToTable("LabSignups");
                });

            modelBuilder.Entity("HFiles_Backend.Domain.Entities.Labs.LabSuperAdmin", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<long>("EpochTime")
                        .HasColumnType("bigint");

                    b.Property<int>("IsMain")
                        .HasColumnType("int");

                    b.Property<int>("LabId")
                        .HasColumnType("int");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("LabSuperAdmins");
                });

            modelBuilder.Entity("HFiles_Backend.Domain.Entities.Labs.LabUserReports", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("BranchId")
                        .HasColumnType("int");

                    b.Property<long>("EpochTime")
                        .HasColumnType("bigint");

                    b.Property<int>("LabId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.Property<int>("Resend")
                        .HasColumnType("int");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("LabUserReports");
                });

            modelBuilder.Entity("HFiles_Backend.Domain.Entities.Labs.UserDetails", b =>
                {
                    b.Property<string>("user_contact")
                        .HasColumnType("longtext");

                    b.Property<string>("user_email")
                        .HasColumnType("longtext");

                    b.Property<string>("user_firstname")
                        .HasColumnType("longtext");

                    b.Property<int>("user_id")
                        .HasColumnType("int");

                    b.Property<string>("user_image")
                        .HasColumnType("longtext");

                    b.Property<string>("user_lastname")
                        .HasColumnType("longtext");

                    b.Property<string>("user_membernumber")
                        .HasColumnType("longtext");

                    b.Property<string>("user_reference")
                        .HasColumnType("longtext");

                    b.ToTable("user_details");
                });

            modelBuilder.Entity("HFiles_Backend.Domain.Entities.Labs.UserReports", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("AccessMappingId")
                        .HasColumnType("longtext");

                    b.Property<DateTime>("CreatedDate")
                        .HasColumnType("datetime(6)");

                    b.Property<double>("FileSize")
                        .HasColumnType("double");

                    b.Property<string>("IsActive")
                        .HasColumnType("longtext");

                    b.Property<int?>("LabId")
                        .HasColumnType("int");

                    b.Property<int?>("LabUserReportId")
                        .HasColumnType("int");

                    b.Property<string>("MemberId")
                        .HasColumnType("longtext");

                    b.Property<string>("NewIsActive")
                        .HasColumnType("longtext");

                    b.Property<int>("ReportId")
                        .HasColumnType("int");

                    b.Property<string>("ReportName")
                        .HasColumnType("longtext");

                    b.Property<string>("ReportUrl")
                        .HasColumnType("longtext");

                    b.Property<string>("UploadType")
                        .HasColumnType("longtext");

                    b.Property<string>("UploadedBy")
                        .HasColumnType("longtext")
                        .HasColumnName("UploadedBy");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("user_reports");
                });
#pragma warning restore 612, 618
        }
    }
}

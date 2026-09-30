using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
using Xunit;

namespace Nagorik.Tests;

public class ReportValidatorTests
{
    static readonly byte[] Jpg =
    {
        0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0
    };

    static readonly byte[] Png =
    {
        0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0
    };

    [Fact]
    public void ValidJpg_Accepted() =>
        Assert.Null(
            ReportValidator.ValidatePhoto("a.jpg", 1000, Jpg));

    [Fact]
    public void ValidPng_Accepted() =>
        Assert.Null(
            ReportValidator.ValidatePhoto("a.PNG", 1000, Png));

    [Fact]
    public void Gif_Rejected() =>
        Assert.NotNull(
            ReportValidator.ValidatePhoto("a.gif", 1000, Jpg));

    [Fact]
    public void OverFiveMb_Rejected() =>
        Assert.NotNull(
            ReportValidator.ValidatePhoto(
                "a.jpg",
                5 * 1024 * 1024 + 1,
                Jpg));

    [Fact]
    public void ExactlyFiveMb_Accepted() =>
        Assert.Null(
            ReportValidator.ValidatePhoto(
                "a.jpg",
                5 * 1024 * 1024,
                Jpg));

    [Fact]
    public void FakeExtension_Rejected() =>
        Assert.NotNull(
            ReportValidator.ValidatePhoto(
                "virus.jpg",
                1000,
                new byte[] { 0x4D, 0x5A, 0, 0 }));

    [Fact]
    public void EmptyFile_Rejected() =>
        Assert.NotNull(
            ReportValidator.ValidatePhoto(
                "a.jpg",
                0,
                Jpg));

    [Theory]
    [InlineData(23.8103, 90.4125, true)]
    [InlineData(22.3569, 91.7832, false)]
    [InlineData(23.8, 90.9, false)]
    public void ServiceArea(
        double lat,
        double lng,
        bool expected) =>
        Assert.Equal(
            expected,
            ReportValidator.IsInDhaka(lat, lng));

    [Fact]
    public void Description501Chars_FailsDataAnnotations()
    {
        var req = new CreateReportRequest
        {
            Category = ReportCategory.Complaint,
            Description = new string('a', 501),
            Latitude = 23.8,
            Longitude = 90.4,
            AddressText = "x",
            SubmissionToken = "t"
        };

        var results = new List<ValidationResult>();

        Assert.False(
            Validator.TryValidateObject(
                req,
                new ValidationContext(req),
                results,
                true));
    }
}
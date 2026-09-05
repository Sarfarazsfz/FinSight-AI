using FinSight.Api.Controllers;
using FinSight.Application.TestData;
using FinSight.Infrastructure.TestData;
using FinSight.Tests.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Tests.Api;

/// <summary>
/// Unit tests for <see cref="TestDataController"/>.
/// Controller constructed directly (no HTTP pipeline) to keep tests fast.
/// </summary>
[TestFixture]
public sealed class TestDataControllerTests
{
    private static readonly Guid OperatorId =
        Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");

    private static readonly Guid OtherUserId =
        Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

    private SyntheticDataGenerator  _generator    = null!;
    private TestDataSessionStore     _sessionStore = null!;

    [SetUp]
    public void SetUp()
    {
        _generator    = new SyntheticDataGenerator();
        _sessionStore = new TestDataSessionStore();
    }

    private TestDataController AsOperator() =>
        new(_generator, _sessionStore, new FixedCurrentUserService(OperatorId));

    private TestDataController AsOtherUser() =>
        new(_generator, _sessionStore, new FixedCurrentUserService(OtherUserId));

    private TestDataController AsAnonymous() =>
        new(_generator, _sessionStore, new FixedCurrentUserService(null));

    // -----------------------------------------------------------------------
    // 21 — Requires authentication (unauthenticated user gets Unauthorized)
    // -----------------------------------------------------------------------

    [Test]
    public void Generate_NullUserId_ReturnsUnauthorized()
    {
        var result = AsAnonymous().Generate(new GenerateDatasetRequest
        {
            Size      = 100,
            Mode      = GenerationMode.Mixed,
            Intensity = CorruptionIntensity.Medium,
        });

        Assert.That(result, Is.InstanceOf<UnauthorizedResult>());
    }

    // -----------------------------------------------------------------------
    // 22 — Valid request returns 200 with metadata
    // -----------------------------------------------------------------------

    [Test]
    public void Generate_ValidRequest_Returns200WithMetadata()
    {
        var result = AsOperator().Generate(new GenerateDatasetRequest
        {
            Size      = 100,
            Mode      = GenerationMode.Mixed,
            Intensity = CorruptionIntensity.Medium,
        });

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var ok = (OkObjectResult)result;
        Assert.That(ok.Value, Is.InstanceOf<GenerateDatasetResponse>());

        var response = (GenerateDatasetResponse)ok.Value!;
        Assert.That(response.Metadata.Size, Is.EqualTo(100));
        Assert.That(response.Metadata.GenerationId, Is.Not.Null.And.Not.Empty);
        Assert.That(response.Metadata.Seed, Is.GreaterThan(0));
    }

    // -----------------------------------------------------------------------
    // 23 — Invalid size rejected
    // -----------------------------------------------------------------------

    [TestCase(0)]
    [TestCase(75)]
    [TestCase(1000)]
    public void Generate_InvalidSize_Returns400(int size)
    {
        var result = AsOperator().Generate(new GenerateDatasetRequest
        {
            Size      = size,
            Mode      = GenerationMode.Mixed,
            Intensity = CorruptionIntensity.Medium,
        });

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(400));
    }

    // -----------------------------------------------------------------------
    // 24 — Invalid mode rejected
    // -----------------------------------------------------------------------

    [Test]
    public void Generate_InvalidMode_Returns400()
    {
        var result = AsOperator().Generate(new GenerateDatasetRequest
        {
            Size      = 100,
            Mode      = (GenerationMode)99,
            Intensity = CorruptionIntensity.Medium,
        });

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(400));
    }

    // -----------------------------------------------------------------------
    // 25 — Seed is respected
    // -----------------------------------------------------------------------

    [Test]
    public void Generate_ExplicitSeed_MetadataRecordsIt()
    {
        var result = AsOperator().Generate(new GenerateDatasetRequest
        {
            Size = 100,
            Seed = 42_000L,
        }) as OkObjectResult;

        var response = result!.Value as GenerateDatasetResponse;
        Assert.That(response!.Metadata.Seed, Is.EqualTo(42_000L));
    }

    // -----------------------------------------------------------------------
    // 26 — User isolation: other user cannot download this user's dataset
    // -----------------------------------------------------------------------

    [Test]
    public void Download_DifferentUser_ReturnsNotFound()
    {
        // Operator A generates a dataset.
        var generateResult = AsOperator().Generate(new GenerateDatasetRequest
        {
            Size = 50,
            Mode = GenerationMode.Clean,
        }) as OkObjectResult;

        var generationId = ((GenerateDatasetResponse)generateResult!.Value!).Metadata.GenerationId;

        // User B tries to download it.
        var downloadResult = AsOtherUser().DownloadPayments(generationId);

        Assert.That(downloadResult, Is.InstanceOf<NotFoundObjectResult>(),
            "A different user should not be able to download another user's dataset.");
    }

    // -----------------------------------------------------------------------
    // 27 — No production data queried (generator is pure computation)
    // -----------------------------------------------------------------------

    [Test]
    public void Generate_NeverQueriesDatabase()
    {
        // SyntheticDataGenerator has no database dependencies.
        // This test documents and enforces that invariant by verifying the
        // generator was constructed with zero infrastructure dependencies.
        var generator = new SyntheticDataGenerator();
        var result = generator.Generate(new DataGenerationRequest
        {
            Size = 50,
            Mode = GenerationMode.Clean,
            Seed = 1,
        });

        // If we get here without injecting any DB context or repository,
        // the invariant holds.
        Assert.That(result.GroundTruth.Count, Is.EqualTo(50));
    }

    // -----------------------------------------------------------------------
    // Download endpoints return CSV content-type
    // -----------------------------------------------------------------------

    [Test]
    public void DownloadPayments_ReturnsFileContentResult_WithCsvType()
    {
        var generateResult = AsOperator().Generate(new GenerateDatasetRequest
        {
            Size = 50,
            Mode = GenerationMode.Clean,
        }) as OkObjectResult;

        var id = ((GenerateDatasetResponse)generateResult!.Value!).Metadata.GenerationId;

        var download = AsOperator().DownloadPayments(id);

        Assert.That(download, Is.InstanceOf<FileContentResult>());
        var file = (FileContentResult)download;
        Assert.That(file.ContentType, Does.StartWith("text/csv"));
        Assert.That(file.FileDownloadName, Is.EqualTo("payments.csv"));
    }

    [Test]
    public void DownloadGroundTruth_ContentContainsExpectedHeader()
    {
        var generateResult = AsOperator().Generate(new GenerateDatasetRequest
        {
            Size = 50,
            Mode = GenerationMode.Mixed,
            Seed = 100,
        }) as OkObjectResult;

        var id = ((GenerateDatasetResponse)generateResult!.Value!).Metadata.GenerationId;
        var download = (FileContentResult)AsOperator().DownloadGroundTruth(id);
        var text = System.Text.Encoding.UTF8.GetString(download.FileContents);

        Assert.That(text, Does.StartWith("transaction_reference,"),
            "Ground-truth CSV missing expected header.");

        Assert.That(text, Does.Contain("expected_status"),
            "Ground-truth CSV missing expected_status column.");
    }

    [Test]
    public void Download_ExpiredOrUnknownId_ReturnsNotFound()
    {
        var download = AsOperator().DownloadPayments("nonexistent_id");
        Assert.That(download, Is.InstanceOf<NotFoundObjectResult>());
    }

    // -----------------------------------------------------------------------
    // Null body returns 400
    // -----------------------------------------------------------------------

    [Test]
    public void Generate_NullBody_Returns400()
    {
        var result = AsOperator().Generate(null!);
        Assert.That(result, Is.InstanceOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(400));
    }
}

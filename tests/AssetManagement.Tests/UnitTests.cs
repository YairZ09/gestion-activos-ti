using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using AssetManagement.Api.Errors;
using AssetManagement.Api.Models;
using AssetManagement.Api.Repositories;
using AssetManagement.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssetManagement.Tests;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new(iterations: 1000);

    [Fact]
    public void Hash_is_not_plaintext_and_verifies()
    {
        var hash = _hasher.Hash("S3creta!");
        Assert.DoesNotContain("S3creta!", hash);
        Assert.StartsWith("v1.", hash);
        Assert.True(_hasher.Verify("S3creta!", hash));
        Assert.False(_hasher.Verify("s3creta!", hash));
    }

    [Fact]
    public void Same_password_produces_different_hashes_because_of_salt()
    {
        Assert.NotEqual(_hasher.Hash("igual"), _hasher.Hash("igual"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto-plano")]
    [InlineData("v1.abc.def.ghi")]
    [InlineData("v1.1000.no-es-base64.tampoco")]
    public void Verify_returns_false_for_malformed_hashes(string stored)
    {
        Assert.False(_hasher.Verify("x", stored));
    }

    [Fact]
    public void Hash_with_default_iterations_can_be_verified_by_other_instance()
    {
        var hash = new PasswordHasher().Hash("clave");
        Assert.True(new PasswordHasher(iterations: 5).Verify("clave", hash));
    }
}

public sealed class RequestValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static CreateAssetRequest ValidAsset() => new()
    {
        AssetCode = "TI-0001001", Category = "Laptop", Brand = "DELL", Model = "5440", OwnershipType = "Propio", CurrentLocation = "Almacen"
    };

    [Fact]
    public void Valid_asset_passes() => Assert.Empty(Validate(ValidAsset()));

    [Fact]
    public void Rented_asset_without_supplier_fails()
    {
        var asset = ValidAsset();
        asset.OwnershipType = "Arrendado";
        Assert.Contains(Validate(asset), r => r.MemberNames.Contains(nameof(CreateAssetRequest.SupplierId)));
        asset.SupplierId = 3;
        Assert.Empty(Validate(asset));
    }

    [Fact]
    public void Rental_end_date_cannot_precede_purchase_date()
    {
        var asset = ValidAsset();
        asset.OwnershipType = "Arrendado";
        asset.SupplierId = 1;
        asset.PurchaseDate = new DateOnly(2026, 5, 1);
        asset.RentalEndDate = new DateOnly(2026, 4, 1);
        Assert.Contains(Validate(asset), r => r.MemberNames.Contains(nameof(CreateAssetRequest.RentalEndDate)));
    }

    [Fact]
    public void Whitespace_only_required_fields_are_rejected()
    {
        var asset = ValidAsset();
        asset.Brand = "   ";
        Assert.NotEmpty(Validate(asset));
    }

    [Theory]
    [InlineData("ti 001")]
    [InlineData("TI;DROP")]
    [InlineData("-TI001")]
    [InlineData("AB")]
    public void Asset_code_format_is_enforced(string code)
    {
        var asset = ValidAsset();
        asset.AssetCode = code;
        Assert.NotEmpty(Validate(asset));
    }

    [Fact]
    public void Status_must_be_one_of_the_allowed_values()
    {
        var update = new UpdateAssetRequest { Category = "L", Brand = "B", Model = "M", OwnershipType = "Propio", CurrentLocation = "A", Status = "Perdido" };
        Assert.NotEmpty(Validate(update));
        update.Status = "Retirado";
        Assert.Empty(Validate(update));
    }
}

public sealed class GlobalExceptionHandlerTests
{
    private static async Task<(int Status, string Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, services.GetRequiredService<IProblemDetailsService>());

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync());
    }

    [Fact]
    public async Task Unexpected_exceptions_return_generic_500_without_internal_details()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("Server=prod-db;Password=SuperSecreto123; tabla Users"));

        Assert.Equal(500, status);
        Assert.DoesNotContain("SuperSecreto123", body);
        Assert.DoesNotContain("prod-db", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("INTERNAL_ERROR", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Business_exceptions_keep_their_status_and_code()
    {
        var (status, body) = await HandleAsync(new BusinessException(409, "ASSET_NOT_AVAILABLE", "No disponible"));

        Assert.Equal(409, status);
        Assert.Contains("ASSET_NOT_AVAILABLE", body);
    }

    [Theory]
    [InlineData(50003, 409, "ASSET_NOT_AVAILABLE")]
    [InlineData(50006, 409, "ASSET_ALREADY_ASSIGNED")]
    [InlineData(50001, 404, "ASSET_NOT_FOUND")]
    [InlineData(50004, 409, "EMPLOYEE_INACTIVE")]
    public void Sql_error_numbers_map_to_controlled_errors(int number, int status, string code)
    {
        var translated = SqlErrorTranslator.Translate(number, "texto interno que no debe salir");

        Assert.NotNull(translated);
        Assert.Equal(status, translated.StatusCode);
        Assert.Equal(code, translated.Code);
        Assert.DoesNotContain("interno", translated.Message);
    }

    [Fact]
    public void Unknown_sql_errors_are_not_translated_so_they_become_generic_500()
    {
        Assert.Null(SqlErrorTranslator.Translate(208, "Invalid object name dbo.Secret"));
    }
}

public sealed class SqlErrorTranslatorTests
{
    [Theory]
    [InlineData(2601, "Cannot insert duplicate key ... unique index UX_Assets_AssetCode.", "ASSET_CODE_DUPLICATE")]
    [InlineData(2627, "Violation of UNIQUE KEY constraint UX_Employees_EmployeeNumber.", "EMPLOYEE_NUMBER_DUPLICATE")]
    [InlineData(2601, "... unique index UX_Assignments_ActiveAsset ...", "ASSET_ALREADY_ASSIGNED")]
    [InlineData(2601, "... unique index IX_Otro ...", "DUPLICATE_VALUE")]
    public void Unique_violations_map_to_friendly_codes(int number, string message, string code)
    {
        var translated = SqlErrorTranslator.Translate(number, message);
        Assert.Equal(409, translated!.StatusCode);
        Assert.Equal(code, translated.Code);
        Assert.DoesNotContain("UX_", translated.Message);
    }
}

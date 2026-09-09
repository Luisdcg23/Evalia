using System.Reflection;
using EBR.Domain.Workflow;
using EBR.Infrastructure.Identity;

namespace EBR.UnitTests;

public sealed class DocumentMetadataTests
{
    [Fact]
    public void UserRegistrationDocumentHasNoBinaryProperties()
    {
        AssertNoBinaryProperties(typeof(UserRegistrationDocument));
    }

    [Fact]
    public void BpmRequestDocumentHasNoBinaryProperties()
    {
        AssertNoBinaryProperties(typeof(BpmRequestDocument));
    }

    private static readonly string[] ExpectedRegistrationDocumentTypes = ["CARTA_AUTORIZACION"];

    [Fact]
    public void UserRegistrationDocumentTypesExposesOnlyAuthorizationLetter()
    {
        Assert.Equal(ExpectedRegistrationDocumentTypes, UserRegistrationDocumentTypes.All);
        Assert.Equal("CARTA_AUTORIZACION", UserRegistrationDocumentTypes.AuthorizationLetter);
    }

    private static void AssertNoBinaryProperties(Type type)
    {
        var binaryProperties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(byte[]) || property.PropertyType == typeof(Stream))
            .Select(property => property.Name)
            .ToArray();

        Assert.True(binaryProperties.Length == 0,
            $"{type.Name} no debe almacenar binarios; propiedades encontradas: {string.Join(", ", binaryProperties)}");
    }
}

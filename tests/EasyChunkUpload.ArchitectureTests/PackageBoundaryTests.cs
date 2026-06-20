using System.Reflection;

namespace EasyChunkUpload.ArchitectureTests;

public sealed class PackageBoundaryTests
{
    [Fact]
    public void Core_DoesNotReferenceInfrastructurePackages()
    {
        var references = typeof(UploadOptions).Assembly
            .GetReferencedAssemblies()
            .Select(static value => value.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("Microsoft.Extensions.Hosting.Abstractions", references);
        Assert.DoesNotContain("EasyChunkUpload.Storage.FileSystem", references);
        Assert.DoesNotContain("EasyChunkUpload.Persistence.EntityFrameworkCore", references);
    }

    [Fact]
    public void InfrastructureImplementations_AreNotPublic()
    {
        AssertInternal(typeof(EasyChunkUpload.Storage.FileSystem.FileSystemStorageOptions).Assembly, "SharedFileSystemChunkStorage");
        AssertInternal(typeof(EasyChunkUpload.Persistence.EntityFrameworkCore.UploadDbContext).Assembly, "EntityFrameworkUploadSessionStore");
        AssertInternal(typeof(EasyChunkUpload.Hosting.UploadMaintenanceOptions).Assembly, "UploadMaintenanceWorker");
    }

    private static void AssertInternal(Assembly assembly, string typeName)
    {
        var type = assembly.GetTypes().Single(value => value.Name == typeName);
        Assert.False(type.IsPublic);
    }
}

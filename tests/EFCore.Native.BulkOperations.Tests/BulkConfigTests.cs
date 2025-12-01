using FluentAssertions;
using Xunit;

namespace EFCore.Native.BulkOperations.Tests;

/// <summary>
/// Unit tests for BulkConfig class.
/// </summary>
public class BulkConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var config = new BulkConfig();

        // Assert
        config.SetOutputIdentity.Should().BeFalse();
        config.PropertiesToInclude.Should().BeNull();
        config.PropertiesToExclude.Should().BeNull();
        config.BatchSize.Should().Be(10000);
        config.BulkCopyTimeout.Should().BeNull();
        config.EnableStreaming.Should().BeTrue();
        config.PreserveInsertOrder.Should().BeFalse();
        config.UpdateByProperties.Should().BeNull();
        config.UseTableLock.Should().BeFalse();
        config.NotifyAfter.Should().BeNull();
        config.NotifyAfterRows.Should().Be(1000);
    }

    [Fact]
    public void SetOutputIdentity_CanBeSet()
    {
        // Arrange & Act
        var config = new BulkConfig { SetOutputIdentity = true };

        // Assert
        config.SetOutputIdentity.Should().BeTrue();
    }

    [Fact]
    public void PropertiesToInclude_CanBeSet()
    {
        // Arrange & Act
        var config = new BulkConfig
        {
            PropertiesToInclude = new List<string> { "Name", "Price" }
        };

        // Assert
        config.PropertiesToInclude.Should().Contain("Name");
        config.PropertiesToInclude.Should().Contain("Price");
        config.PropertiesToInclude.Should().HaveCount(2);
    }

    [Fact]
    public void PropertiesToExclude_CanBeSet()
    {
        // Arrange & Act
        var config = new BulkConfig
        {
            PropertiesToExclude = new List<string> { "CreatedDate" }
        };

        // Assert
        config.PropertiesToExclude.Should().Contain("CreatedDate");
        config.PropertiesToExclude.Should().HaveCount(1);
    }

    [Fact]
    public void BatchSize_CanBeSet()
    {
        // Arrange & Act
        var config = new BulkConfig { BatchSize = 5000 };

        // Assert
        config.BatchSize.Should().Be(5000);
    }

    [Fact]
    public void BulkCopyTimeout_CanBeSet()
    {
        // Arrange & Act
        var config = new BulkConfig { BulkCopyTimeout = 300 };

        // Assert
        config.BulkCopyTimeout.Should().Be(300);
    }

    [Fact]
    public void UpdateByProperties_CanBeSet()
    {
        // Arrange & Act
        var config = new BulkConfig
        {
            UpdateByProperties = new List<string> { "ProductCode" }
        };

        // Assert
        config.UpdateByProperties.Should().Contain("ProductCode");
        config.UpdateByProperties.Should().HaveCount(1);
    }

    [Fact]
    public void NotifyAfter_CanBeSet()
    {
        // Arrange
        var notifyCount = 0L;
        Action<long> callback = count => notifyCount = count;

        // Act
        var config = new BulkConfig { NotifyAfter = callback };
        config.NotifyAfter!(1000);

        // Assert
        notifyCount.Should().Be(1000);
    }
}

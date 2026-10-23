using System;
using FluentAssertions;
using VirtoCommerce.ProductSubscription.Core.Models;
using Xunit;

namespace VirtoCommerce.ProductSubscription.Tests.Core;

[Trait("Category", "Unit")]
public class RenewalTests
{
    [Fact]
    public void NewRenewal_StartsScheduledAndOpen()
    {
        // Act
        var renewal = new Renewal();

        // Assert
        renewal.State.Should().Be(RenewalState.Scheduled);
        renewal.IsOpen.Should().BeTrue();
        renewal.Events.Should().BeEmpty();
        renewal.Items.Should().BeEmpty();
    }

    [Fact]
    public void AddEvent_RecordsEventWithCurrentState()
    {
        // Arrange
        var renewal = new Renewal { State = RenewalState.Notified };
        var before = DateTime.UtcNow;

        // Act
        renewal.AddEvent(RenewalEventType.ReminderSent, "Reminder sent", "to customer@example.com");

        // Assert
        var evt = renewal.Events.Should().ContainSingle().Subject;
        evt.State.Should().Be(RenewalState.Notified);
        evt.EventType.Should().Be(RenewalEventType.ReminderSent);
        evt.Message.Should().Be("Reminder sent");
        evt.Details.Should().Be("to customer@example.com");
        evt.EventDate.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        evt.EventDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void AddEvent_NullEvents_CreatesList()
    {
        // Arrange
        var renewal = new Renewal { Events = null };

        // Act
        renewal.AddEvent(RenewalEventType.Error, "Boom");

        // Assert
        renewal.Events.Should().ContainSingle().Which.Details.Should().BeNull();
    }

    [Fact]
    public void AddEvent_AppendsInOrder()
    {
        // Arrange
        var renewal = new Renewal();

        // Act
        renewal.AddEvent(RenewalEventType.Calculated, "first");
        renewal.AddEvent(RenewalEventType.PaymentAttempt, "second");

        // Assert
        renewal.Events.Should().HaveCount(2);
        renewal.Events[0].Message.Should().Be("first");
        renewal.Events[1].Message.Should().Be("second");
    }

    [Fact]
    public void ChangeState_SetsStateAndNextProcessingDate_AndLogsTransition()
    {
        // Arrange
        var renewal = new Renewal { State = RenewalState.Scheduled };
        var next = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        renewal.ChangeState(RenewalState.Notified, "Reminder sent", next);

        // Assert
        renewal.State.Should().Be(RenewalState.Notified);
        renewal.NextProcessingDate.Should().Be(next);

        var evt = renewal.Events.Should().ContainSingle().Subject;
        evt.EventType.Should().Be(RenewalEventType.StateChanged);
        evt.Message.Should().Be("Reminder sent");
        evt.Details.Should().Be("Scheduled → Notified");
        // The event is added after the state is changed, so it carries the new state.
        evt.State.Should().Be(RenewalState.Notified);
    }

    [Fact]
    public void ChangeState_WithoutNextProcessingDate_ClearsIt()
    {
        // Arrange
        var renewal = new Renewal
        {
            State = RenewalState.Authorized,
            NextProcessingDate = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        // Act
        renewal.ChangeState(RenewalState.Completed, "Captured");

        // Assert
        renewal.NextProcessingDate.Should().BeNull();
        renewal.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void ChangeState_SameState_StillLogsEvent()
    {
        // Arrange
        var renewal = new Renewal { State = RenewalState.RetryScheduled };

        // Act
        renewal.ChangeState(RenewalState.RetryScheduled, "Retry again");

        // Assert
        renewal.Events.Should().ContainSingle().Which.Details.Should().Be("RetryScheduled → RetryScheduled");
    }

    [Theory]
    [InlineData(RenewalState.Completed)]
    [InlineData(RenewalState.Failed)]
    [InlineData(RenewalState.Skipped)]
    [InlineData(RenewalState.Cancelled)]
    public void IsOpen_FinalState_False(string state)
    {
        new Renewal { State = state }.IsOpen.Should().BeFalse();
    }

    [Theory]
    [InlineData(RenewalState.Scheduled)]
    [InlineData(RenewalState.Notified)]
    [InlineData(RenewalState.ReviewRequired)]
    [InlineData(RenewalState.Ordered)]
    [InlineData(RenewalState.PaymentActionRequired)]
    [InlineData(RenewalState.RetryScheduled)]
    [InlineData(RenewalState.Authorized)]
    public void IsOpen_NonFinalState_True(string state)
    {
        new Renewal { State = state }.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void Final_ContainsExactlyTerminalStates()
    {
        RenewalState.Final.Should().BeEquivalentTo(
            [RenewalState.Completed, RenewalState.Failed, RenewalState.Skipped, RenewalState.Cancelled]);
    }

    [Fact]
    public void BeforeOrder_ContainsExactlyStatesBeforeTheOrderIsPlaced()
    {
        RenewalState.BeforeOrder.Should().BeEquivalentTo(
            [RenewalState.Scheduled, RenewalState.Notified, RenewalState.ReviewRequired]);
    }

    [Fact]
    public void StateGroups_AreSubsetsOfAll_AndDoNotOverlap()
    {
        RenewalState.All.Should().OnlyHaveUniqueItems();
        RenewalState.Final.Should().BeSubsetOf(RenewalState.All);
        RenewalState.BeforeOrder.Should().BeSubsetOf(RenewalState.All);
        RenewalState.BeforeOrder.Should().NotIntersectWith(RenewalState.Final);
    }

    [Fact]
    public void Clone_DeepCopiesItemsAndEvents()
    {
        // Arrange
        var renewal = new Renewal { Number = "R-1" };
        renewal.Items.Add(new RenewalItem { ProductId = "P1", Quantity = 1 });
        renewal.AddEvent(RenewalEventType.Calculated, "calc");

        // Act
        var clone = (Renewal)renewal.Clone();
        clone.Items[0].Quantity = 5;
        clone.AddEvent(RenewalEventType.Error, "only on clone");

        // Assert
        clone.Number.Should().Be("R-1");
        renewal.Items[0].Quantity.Should().Be(1);
        renewal.Events.Should().ContainSingle();
        clone.Events.Should().HaveCount(2);
    }
}

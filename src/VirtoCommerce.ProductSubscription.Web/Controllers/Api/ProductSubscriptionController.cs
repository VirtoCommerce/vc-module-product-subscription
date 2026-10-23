using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PaymentModule.Core.Model.StoredCredentials;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.ProductSubscription.Core;
using VirtoCommerce.ProductSubscription.Core.Models;
using VirtoCommerce.ProductSubscription.Core.Services;
using VirtoCommerce.ProductSubscription.Data.Import;
using Permissions = VirtoCommerce.ProductSubscription.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.ProductSubscription.Web.Controllers.Api;

/// <summary>
/// Admin and integration API for subscriptions. Customers use xAPI (GraphQL), which checks ownership on every call.
/// </summary>
[Route("api/product-subscription")]
[Authorize]
public class ProductSubscriptionController(
    ISubscriptionPlanService planService,
    ISubscriptionPlanSearchService planSearchService,
    ISubscriptionService subscriptionService,
    ISubscriptionSearchService subscriptionSearchService,
    IRenewalService renewalService,
    IRenewalSearchService renewalSearchService,
    ISubscriptionManager subscriptionManager,
    IRenewalProcessor renewalProcessor,
    ISubscriptionOfferService offerService,
    ISubscriptionTermsService termsService,
    IStoredPaymentMethodSearchService storedPaymentMethodSearchService,
    IPaymentConsentService consentService,
    ILegacySubscriptionImporter legacySubscriptionImporter,
    LegacySubscriptionJsonMapper legacySubscriptionJsonMapper)
    : Controller
{
    #region Plans

    [HttpPost("plans/search")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<SubscriptionPlanSearchResult>> SearchPlans([FromBody] SubscriptionPlanSearchCriteria criteria)
    {
        return Ok(await planSearchService.SearchAsync(criteria));
    }

    [HttpGet("plans/{id}")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<SubscriptionPlan>> GetPlan([FromRoute] string id)
    {
        var plan = await planService.GetByIdAsync(id);
        return plan == null ? NotFound() : Ok(plan);
    }

    [HttpPost("plans")]
    [Authorize(Permissions.ManagePlans)]
    public async Task<ActionResult<SubscriptionPlan>> SavePlan([FromBody] SubscriptionPlan plan)
    {
        var error = ValidatePlan(plan);
        if (error != null)
        {
            return BadRequest(new { message = error });
        }

        await planService.SaveChangesAsync([plan]);
        return Ok(plan);
    }

    [HttpDelete("plans")]
    [Authorize(Permissions.ManagePlans)]
    public async Task<ActionResult> DeletePlans([FromQuery] string[] ids)
    {
        await planService.DeleteAsync(ids);
        return NoContent();
    }

    [HttpGet("offers")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<IList<SubscriptionOffer>>> GetOffers([FromQuery] string storeId, [FromQuery] string[] productIds)
    {
        var offers = await offerService.GetOffersAsync(storeId, productIds);
        return Ok(offers.Values.ToList());
    }

    [HttpGet("terms")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<SubscriptionTerms>> GetTerms([FromQuery] string storeId)
    {
        return Ok(await termsService.GetTermsAsync(storeId));
    }

    #endregion

    #region Subscriptions

    [HttpPost("subscriptions/search")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<SubscriptionSearchResult>> SearchSubscriptions([FromBody] SubscriptionSearchCriteria criteria)
    {
        return Ok(await subscriptionSearchService.SearchAsync(criteria));
    }

    [HttpGet("subscriptions/{id}")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<Subscription>> GetSubscription([FromRoute] string id)
    {
        var subscription = await subscriptionService.GetByIdAsync(id);
        return subscription == null ? NotFound() : Ok(subscription);
    }

    [HttpPut("subscriptions/{id}")]
    [Authorize(Permissions.Update)]
    public Task<ActionResult<Subscription>> ChangeSubscription([FromRoute] string id, [FromBody] SubscriptionChange change)
        => Execute(() => subscriptionManager.ChangeAsync(id, change));

    [HttpPost("subscriptions/{id}/skip")]
    [Authorize(Permissions.Update)]
    public Task<ActionResult<Subscription>> SkipSubscription([FromRoute] string id)
        => Execute(() => subscriptionManager.SkipNextAsync(id));

    [HttpPost("subscriptions/{id}/pause")]
    [Authorize(Permissions.Update)]
    public Task<ActionResult<Subscription>> PauseSubscription([FromRoute] string id, [FromQuery] DateTime? resumeDate)
        => Execute(() => subscriptionManager.PauseAsync(id, resumeDate));

    [HttpPost("subscriptions/{id}/resume")]
    [Authorize(Permissions.Update)]
    public Task<ActionResult<Subscription>> ResumeSubscription([FromRoute] string id)
        => Execute(() => subscriptionManager.ResumeAsync(id));

    [HttpPost("subscriptions/{id}/cancel")]
    [Authorize(Permissions.Update)]
    public Task<ActionResult<Subscription>> CancelSubscription([FromRoute] string id, [FromQuery] string reason)
        => Execute(() => subscriptionManager.CancelAsync(id, reason));

    [HttpPost("subscriptions/{id}/order-now")]
    [Authorize(Permissions.ProcessRenewals)]
    public Task<ActionResult<Renewal>> OrderSubscriptionNow([FromRoute] string id)
        => Execute(() => renewalProcessor.OrderNowAsync(id));

    [HttpDelete("subscriptions")]
    [Authorize(Permissions.Delete)]
    public async Task<ActionResult> DeleteSubscriptions([FromQuery] string[] ids)
    {
        await subscriptionService.DeleteAsync(ids);
        return NoContent();
    }

    [HttpGet("customers/{customerId}/saved-payment-methods")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<IList<StoredPaymentMethod>>> GetSavedPaymentMethods([FromRoute] string customerId, [FromQuery] string storeId)
    {
        var criteria = AbstractTypeFactory<StoredPaymentMethodSearchCriteria>.TryCreateInstance();
        criteria.CustomerId = customerId;
        criteria.StoreId = storeId;
        criteria.Take = 100;
        return Ok((await storedPaymentMethodSearchService.SearchAsync(criteria)).Results);
    }

    [HttpGet("consents/{id}")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<PaymentConsent>> GetConsent([FromRoute] string id)
    {
        var consent = await consentService.GetByIdAsync(id);
        return consent == null ? NotFound() : Ok(consent);
    }

    #endregion

    #region Renewals

    [HttpPost("renewals/search")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<RenewalSearchResult>> SearchRenewals([FromBody] RenewalSearchCriteria criteria)
    {
        return Ok(await renewalSearchService.SearchAsync(criteria));
    }

    [HttpGet("renewals/{id}")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<Renewal>> GetRenewal([FromRoute] string id)
    {
        var renewal = await renewalService.GetByIdAsync(id);
        return renewal == null ? NotFound() : Ok(renewal);
    }

    /// <summary>Advance a renewal right now, ignoring its reminder and cutoff times (for support and testing).</summary>
    [HttpPost("renewals/{id}/process")]
    [Authorize(Permissions.ProcessRenewals)]
    public Task<ActionResult<Renewal>> ProcessRenewal([FromRoute] string id)
        => Execute(() => renewalProcessor.ProcessAsync(id, ignoreSchedule: true));

    [HttpPost("renewals/{id}/skip")]
    [Authorize(Permissions.ProcessRenewals)]
    public Task<ActionResult<Renewal>> SkipRenewal([FromRoute] string id)
        => Execute(() => renewalProcessor.SkipAsync(id));

    [HttpPost("renewals/{id}/accept")]
    [Authorize(Permissions.ProcessRenewals)]
    public Task<ActionResult<Renewal>> AcceptRenewal([FromRoute] string id)
        => Execute(() => renewalProcessor.AcceptReviewAsync(id));

    [HttpPost("renewals/{id}/retry-payment")]
    [Authorize(Permissions.ProcessRenewals)]
    public Task<ActionResult<Renewal>> RetryRenewalPayment([FromRoute] string id)
        => Execute(() => renewalProcessor.RetryPaymentAsync(id));

    [HttpPost("renewals/{id}/cancel")]
    [Authorize(Permissions.ProcessRenewals)]
    public Task<ActionResult<Renewal>> CancelRenewal([FromRoute] string id, [FromQuery] string reason)
        => Execute(() => renewalProcessor.CancelAsync(id, reason));

    /// <summary>Run one scheduler tick now: create due renewals and advance due renewals.</summary>
    [HttpPost("scheduler/run")]
    [Authorize(Permissions.ProcessRenewals)]
    public async Task<ActionResult<object>> RunScheduler()
    {
        var processed = await renewalProcessor.ProcessDueAsync();
        return Ok(new { processed });
    }

    #endregion

    #region Import

    /// <summary>
    /// Import subscriptions from the old subscription module (VirtoCommerce.Subscription). The body is either a JSON array
    /// of <see cref="LegacySubscriptionImportRecord"/>s or the old module's JSON as returned by its
    /// <c>POST api/subscriptions/search</c> (with the WithOrderPrototype response group); the shape is detected.
    /// Run with <paramref name="dryRun"/> = true first: it validates and reports without saving.
    /// </summary>
    [HttpPost("import/legacy")]
    [Authorize(Permissions.Import)]
    public async Task<ActionResult<SubscriptionImportResult>> ImportLegacySubscriptions([FromBody] JToken body, [FromQuery] bool dryRun = true)
    {
        if (body == null || body.Type is JTokenType.Null or JTokenType.Undefined)
        {
            return BadRequest(new { message = "The request body is empty." });
        }

        IList<LegacySubscriptionImportRecord> records;
        try
        {
            records = legacySubscriptionJsonMapper.ReadRecords(body);
        }
        catch (Exception ex) when (ex is Newtonsoft.Json.JsonException or FormatException or ArgumentException)
        {
            return BadRequest(new { message = $"The request body can't be read: {ex.Message}" });
        }

        if (records.Count == 0)
        {
            return BadRequest(new { message = "The request body has no subscriptions to import." });
        }

        return Ok(await legacySubscriptionImporter.ImportAsync(records, dryRun));
    }

    #endregion

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static string ValidatePlan(SubscriptionPlan plan)
    {
        if (plan == null)
        {
            return "The plan is empty.";
        }

        if (string.IsNullOrWhiteSpace(plan.Name))
        {
            return "Enter a plan name.";
        }

        if (string.IsNullOrWhiteSpace(plan.StoreId))
        {
            return "Choose a store.";
        }

        if (plan.Intervals == null || plan.Intervals.Count == 0)
        {
            return "Add at least one delivery frequency.";
        }

        if (plan.Intervals.Any(x => x.Count < 1 || x.Count > 365))
        {
            return "Each frequency must be between 1 and 365.";
        }

        if (plan.Intervals.GroupBy(x => (x.Unit, x.Count)).Any(x => x.Count() > 1))
        {
            return "Each frequency can be listed only once.";
        }

        if (plan.DiscountType == SubscriptionDiscountType.Percent && (plan.DiscountValue < 0 || plan.DiscountValue > 100))
        {
            return "A percent discount must be between 0 and 100.";
        }

        if (plan.DiscountValue < 0)
        {
            return "The discount can't be negative.";
        }

        if (plan.Intervals.Count(x => x.IsDefault) != 1)
        {
            foreach (var interval in plan.Intervals)
            {
                interval.IsDefault = false;
            }

            plan.Intervals[0].IsDefault = true;
        }

        return null;
    }
}

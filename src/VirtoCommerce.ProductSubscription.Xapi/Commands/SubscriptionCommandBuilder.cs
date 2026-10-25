using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.ProductSubscription.Xapi.Queries;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.ProductSubscription.Xapi.Commands;

/// <summary>Base for every subscription mutation: requires a signed-in user and passes the user id to the handler.</summary>
public abstract class SubscriptionCommandBuilder<TCommand, TResult, TInputGraphType, TResultGraphType>(IAuthorizationService authorizationService)
    : CommandBuilder<TCommand, TResult, TInputGraphType, TResultGraphType>(authorizationService)
    where TCommand : IRequest<TResult>, IHasUserId
    where TInputGraphType : IInputObjectGraphType
    where TResultGraphType : IGraphType
{
    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, TCommand request)
    {
        request.UserId = AccessGuard.RequireUser(context);
        await base.BeforeMediatorSend(context, request);
    }
}

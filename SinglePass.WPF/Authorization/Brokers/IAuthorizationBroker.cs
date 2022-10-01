﻿using SinglePass.WPF.Authorization.TokenHolders;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.WPF.Authorization.Brokers
{
    public interface IAuthorizationBroker
    {
        ITokenHolder TokenHolder { get; }
        Task AuthorizeAsync(CancellationToken cancellationToken);
        Task RefreshAccessToken(CancellationToken cancellationToken);
        Task RevokeToken(CancellationToken cancellationToken);
    }
}

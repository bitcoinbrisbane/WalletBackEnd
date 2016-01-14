﻿using NBitcoin;

namespace LykkeWalletServices.Transactions.TaskHandlers
{
    public class SrvNetworkInvolvingExchangeBase : SrvNetworkBase
    {
        protected string ExchangePrivateKey
        {
            get; set;
        }

        public SrvNetworkInvolvingExchangeBase(Network network, OpenAssetsHelper.AssetDefinition[] assets,
            string username, string password, string ipAddress, string exchangePrivateKey, string connectionString)
            : base(network, assets, username, password, ipAddress, connectionString)
        {
            this.ExchangePrivateKey = exchangePrivateKey;
        }
    }
}

﻿using Core;
using NBitcoin;

namespace LykkeWalletServices.Transactions.TaskHandlers
{
    public class SrvNetworkInvolvingExchangeBase : SrvNetworkBase
    {
        protected string ExchangePrivateKey
        {
            get; set;
        }

        public SrvNetworkInvolvingExchangeBase(Network network, AssetDefinition[] assets,
            string username, string password, string ipAddress, string feeAddress, string exchangePrivateKey, string connectionString)
            : base(network, assets, username, password, ipAddress, connectionString, feeAddress)
        {
            ExchangePrivateKey = exchangePrivateKey;
            FeeAddress = feeAddress;
        }
    }
}

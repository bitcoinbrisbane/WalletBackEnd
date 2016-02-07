﻿using Core;
using NBitcoin;
using System;
using System.Threading.Tasks;

namespace LykkeWalletServices.Transactions.TaskHandlers
{
    // Sample input: GenerateNewWallet:{"TransactionId":"10"}
    // Sample output: GenerateNewWallet:{"TransactionId":"10","Result":{"WalletAddress":"mtgbKeNYngWvjmUaSfqhnKD3s2niC3tsCx","WalletPrivateKey":"xxx","MultiSigAddress":"2NDSoShWdKkVmjp9RMTRFsAwCRiNouTL6dv"},"Error":null}
    public class SrvGenerateNewWalletTask
    {
        private Network network = null;
        private string exchangePrivateKey = null;
        private string connectionString = null;
        public SrvGenerateNewWalletTask(Network network, string exchangePrivateKey, string connectionString)
        {
            this.network = network;
            this.exchangePrivateKey = exchangePrivateKey;
            this.connectionString = connectionString;
        }

        public async Task<Tuple<GenerateNewWalletTaskResult, Error>> ExecuteTask()
        {
            GenerateNewWalletTaskResult result = null;
            string walletAddress = null;
            string walletPrivateKey = null;
            string multiSigAddressStorage = null;
            Error error = null;
            try
            {
                Key key = new Key();
                BitcoinSecret secret = new BitcoinSecret(key, this.network);

                walletAddress = secret.GetAddress().ToWif();
                walletPrivateKey = secret.PrivateKey.GetWif(network).ToWif();

                var multiSigAddress = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, new PubKey[] { secret.PubKey ,
                (new BitcoinSecret(exchangePrivateKey, network)).PubKey });
                multiSigAddressStorage = multiSigAddress.GetScriptAddress(network).ToString();

                using (SqlexpressLykkeEntities entitiesContext = new SqlexpressLykkeEntities(connectionString))
                {
                    KeyStorage item = new KeyStorage
                    {
                        WalletAddress = walletAddress,
                        WalletPrivateKey = walletPrivateKey,
                        MultiSigAddress = multiSigAddressStorage,
                        ExchangePrivateKey = exchangePrivateKey,
                        MultiSigScript = multiSigAddress.ToString(),
                        Network = (network == Network.Main ? NetworkType.Main : NetworkType.TestNet).ToString()
                    };

                    entitiesContext.KeyStorages.Add(item);
                    await entitiesContext.SaveChangesAsync();
                }

                result = new GenerateNewWalletTaskResult();
                result.WalletAddress = walletAddress;
                result.WalletPrivateKey = walletPrivateKey;
                result.MultiSigAddress = multiSigAddressStorage;
            }
            catch (Exception e)
            {
                error = new Error();
                error.Code = ErrorCode.Exception;
                error.Message = e.ToString();
            }
            return new Tuple<GenerateNewWalletTaskResult, Error>(result, error);
        }

        public void Execute(TaskToDoGenerateNewWallet data, Func<Tuple<GenerateNewWalletTaskResult, Error>, Task> invokeResult)
        {
            Task.Run(async () =>
            {
                var result = await ExecuteTask();
                await invokeResult(result);
            });
        }
    }
}

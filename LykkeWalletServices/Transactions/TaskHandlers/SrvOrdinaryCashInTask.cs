﻿using Core;
using NBitcoin;
using NBitcoin.OpenAsset;
using System;
using System.Threading.Tasks;

namespace LykkeWalletServices.Transactions.TaskHandlers
{
    // Sample Input: OrdinaryCashIn:{"TransactionId":"10","MultisigAddress":"2NC9qfGybmWgKUdfSebana1HPsAUcXvMmpo","Amount":200,"Currency":"bjkUSD","PrivateKey":"xxx", "PublicWallet":"xxx"}
    // Sample Output: OrdinaryCashIn:{"TransactionId":"10","Result":{"TransactionHex":"xxx","TransactionHash":"xxx"},"Error":null}
    public class SrvOrdinaryCashInTask : SrvNetworkBase
    {
        public SrvOrdinaryCashInTask(Network network, AssetDefinition[] assets, string username,
            string password, string ipAddress, string connectionString, string feeAddress) : base(network, assets, username, password, ipAddress, connectionString, feeAddress)
        {
        }

        public async Task<Tuple<OrdinaryCashInTaskResult, Error>> ExecuteTask(TaskToDoOrdinaryCashIn data)
        {
            OrdinaryCashInTaskResult result = null;
            Error error = null;
            try
            {
                using (SqlexpressLykkeEntities entities = new SqlexpressLykkeEntities(ConnectionString))
                {
                    var MultisigAddress = await OpenAssetsHelper.GetMatchingMultisigAddress(data.MultisigAddress, entities);
                    OpenAssetsHelper.GetOrdinaryCoinsForWalletReturnType walletCoins =
                        (OpenAssetsHelper.GetOrdinaryCoinsForWalletReturnType)await OpenAssetsHelper.GetCoinsForWallet(MultisigAddress.WalletAddress, !OpenAssetsHelper.IsRealAsset(data.Currency) ? Convert.ToInt64(data.Amount * OpenAssetsHelper.BTCToSathoshiMultiplicationFactor) : 0, data.Amount, data.Currency,
                         Assets, Network, Username, Password, IpAddress, ConnectionString, entities, true, false);
                    if (walletCoins.Error != null)
                    {
                        error = walletCoins.Error;
                    }
                    else
                    {
                        using (var transaction = entities.Database.BeginTransaction())
                        {
                            TransactionBuilder builder = new TransactionBuilder();
                            builder
                                .AddKeys(new BitcoinSecret(MultisigAddress.WalletPrivateKey));
                            if (OpenAssetsHelper.IsRealAsset(data.Currency))
                            {
                                builder = (await builder.AddCoins(walletCoins.AssetCoins)
                                    .AddEnoughPaymentFee(entities, Network.ToString(), 2)) // // One of the open assets inputs may not be generated by us, for example coinprism does 600 instead of 2730
                                    .SendAsset(new Script(MultisigAddress.MultiSigScript).GetScriptAddress(Network), new AssetMoney(new AssetId(new BitcoinAssetId(walletCoins.Asset.AssetId, Network)), Convert.ToInt64((data.Amount * walletCoins.Asset.AssetMultiplicationFactor))));
                            }
                            else
                            {
                                builder.AddCoins(walletCoins.Coins);
                                builder.Send(new Script(MultisigAddress.MultiSigScript).GetScriptAddress(Network),
                                    Convert.ToInt64(data.Amount * OpenAssetsHelper.BTCToSathoshiMultiplicationFactor))
                                    .SetChange(new BitcoinPubKeyAddress(MultisigAddress.WalletAddress, Network)).Then();
                                builder = (await builder.AddEnoughPaymentFee(entities, Network.ToString(), 0));
                            }


                            var tx = builder.SendFees(new Money(OpenAssetsHelper.TransactionSendFeesInSatoshi))
                                .SetChange(new BitcoinPubKeyAddress(MultisigAddress.WalletAddress, Network))
                                .BuildTransaction(true, SigHash.All);

                            Error localerror = await OpenAssetsHelper.CheckTransactionForDoubleSpentThenSendIt
                                (tx, Username, Password, IpAddress, Network, entities, ConnectionString);

                            if (localerror == null)
                            {
                                result = new OrdinaryCashInTaskResult
                                {
                                    TransactionHex = tx.ToHex(),
                                    TransactionHash = tx.GetHash().ToString()
                                };
                            }
                            else
                            {
                                error = localerror;
                            }

                            if (error == null)
                            {
                                transaction.Commit();
                            }
                            else
                            {
                                transaction.Rollback();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                error = new Error();
                error.Code = ErrorCode.Exception;
                error.Message = e.ToString();
            }
            return new Tuple<OrdinaryCashInTaskResult, Error>(result, error);
        }

        public void Execute(TaskToDoOrdinaryCashIn data, Func<Tuple<OrdinaryCashInTaskResult, Error>, Task> invokeResult)
        {
            Task.Run(async () =>
            {
                var result = await ExecuteTask(data);
                await invokeResult(result);
            });
        }
    }
}

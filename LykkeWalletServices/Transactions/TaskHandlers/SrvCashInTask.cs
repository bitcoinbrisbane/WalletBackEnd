﻿using Core;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace LykkeWalletServices.Transactions.TaskHandlers
{
    // Sample request: CashIn:{"TransactionId":"10","MultisigAddress":"3NQ6FF3n8jPFyPewMqzi2qYp8Y4p3UEz9B","Amount":5000,"Currency":"bjkUSD"}
    // Sample response CashIn:{"TransactionId":"10","Result":{"TransactionHex":"xxx","TransactionHash":"xxx"},"Error":null}
    public class SrvCashInTask : SrvNetworkBase
    {
        public SrvCashInTask(Network network, OpenAssetsHelper.AssetDefinition[] assets, string username,
            string password, string ipAddress, string connectionString) : base(network, assets, username, password, ipAddress, connectionString)
        {
        }

        public async Task<Tuple<CashInTaskResult, Error>> ExecuteTask(TaskToDoCashIn data)
        {
            CashInTaskResult result = null;
            Error error = null;
            try
            {
                var asset = OpenAssetsHelper.GetAssetFromName(Assets, data.Currency, Network);
                OpenAssetsHelper.GetOrdinaryCoinsForWalletReturnType walletCoins = (OpenAssetsHelper.GetOrdinaryCoinsForWalletReturnType)await OpenAssetsHelper.GetCoinsForWallet(asset.AssetAddress.ToString(), 0, data.Currency,
                    Assets, Network, Username, Password, IpAddress, ConnectionString, true, false);
                if (walletCoins.Error != null)
                {
                    error = walletCoins.Error;
                }

                else
                {
                    var coins = walletCoins.Coins;
                    IssuanceCoin issueCoin = new IssuanceCoin(coins.Last());
                    issueCoin.DefinitionUrl = new Uri(walletCoins.Asset.AssetDefinitionUrl);
                    var txCoins = coins.Take(coins.Length - 1);

                    var multiSigScript = new Script((await OpenAssetsHelper.GetMatchingMultisigAddress(data.MultisigAddress, ConnectionString)).MultiSigScript);
                    // Issuing the asset
                    TransactionBuilder builder = new TransactionBuilder();
                    var tx = builder
                        .AddKeys(new BitcoinSecret(walletCoins.Asset.AssetPrivateKey))
                        .AddCoins(issueCoin)
                        .AddCoins(txCoins)
                        .IssueAsset(multiSigScript.GetScriptAddress(Network), new NBitcoin.OpenAsset.AssetMoney(
                            new NBitcoin.OpenAsset.AssetId(new NBitcoin.OpenAsset.BitcoinAssetId(walletCoins.Asset.AssetId, Network)),
                            //   (long) (data.Amount * assetMultiplicationFactor))) // if data.Amount * assetMultiplicationFactor is 43 the (long)() returns 43
                            Convert.ToInt64(data.Amount * walletCoins.Asset.AssetMultiplicationFactor)))
                        .SendFees(new Money(OpenAssetsHelper.TransactionSendFeesInSatoshi))
                        .SetChange(walletCoins.Asset.AssetAddress)
                        .BuildTransaction(true);

                    Error localerror = await OpenAssetsHelper.CheckTransactionForDoubleSpentThenSendIt
                        (tx, Username, Password, IpAddress, Network, ConnectionString);

                    if (localerror == null)
                    {
                        result = new CashInTaskResult
                        {
                            TransactionHex = tx.ToHex(),
                            TransactionHash = tx.GetHash().ToString()
                        };
                    }
                    else
                    {
                        error = localerror;
                    }
                }

                /*
                using (SqliteLykkeServicesEntities entitiesContext = new SqliteLykkeServicesEntities())
                {
                    var matchingAddress = await (from item in entitiesContext.KeyStorages
                                                 where item.MultiSigAddress.Equals(data.MultisigAddress)
                                                 select item).SingleOrDefaultAsync();
                    if (matchingAddress == null)
                    {
                        throw new Exception("Could not find a matching record for MultiSigAddress: " + data.MultisigAddress);
                    }

                    string assetId = null;
                    string assetPrivateKey = null;
                    BitcoinAddress assetAddress = null;
                    string assetDefinitionUrl = null;
                    long assetMultiplicationFactor = 1;


                    // Getting the assetid from asset name
                    foreach (var item in Assets)
                    {
                        if (item.Name == data.Currency)
                        {
                            assetId = item.AssetId;
                            assetPrivateKey = item.PrivateKey;
                            assetAddress = (new BitcoinSecret(assetPrivateKey, Network)).PubKey.
                                GetAddress(Network);
                            assetDefinitionUrl = item.DefinitionUrl;
                            assetMultiplicationFactor = item.MultiplyFactor;
                            break;
                        }
                    }

                    var walletOutputs = await OpenAssetsHelper.GetWalletOutputs(assetAddress.ToString(), Network);
                    if (walletOutputs.Item2)
                    {
                        error = new Error();
                        error.Code = ErrorCode.ProblemInRetrivingWalletOutput;
                        error.Message = walletOutputs.Item3;

                    }
                    else
                    {
                        var bitcoinOutputs = OpenAssetsHelper.GetWalletOutputsUncolored(walletOutputs.Item1);
                        if (!OpenAssetsHelper.IsBitcoinsEnough(bitcoinOutputs, OpenAssetsHelper.MinimumRequiredSatoshi))
                        {
                            error = new Error();
                            error.Code = ErrorCode.NotEnoughBitcoinInTransaction;
                            error.Message = "The required amount of satoshis to send transaction is " + OpenAssetsHelper.MinimumRequiredSatoshi +
                                " . The address is: " + assetAddress;
                        }
                        else
                        {
                            var coins = (await OpenAssetsHelper.GetColoredUnColoredCoins(bitcoinOutputs, null, Network,
                                Username, Password, IpAddress)).Item2;
                            IssuanceCoin issueCoin = new IssuanceCoin(coins.Last());
                            issueCoin.DefinitionUrl = new Uri(assetDefinitionUrl);
                            var txCoins = coins.Take(coins.Length - 1);

                            var multiSigScript = new Script(matchingAddress.MultiSigScript);
                            // Issuing the asset
                            TransactionBuilder builder = new TransactionBuilder();
                            var tx = builder
                                .AddKeys(new BitcoinSecret(assetPrivateKey))
                                .AddCoins(issueCoin)
                                .AddCoins(txCoins)
                                .IssueAsset(multiSigScript.GetScriptAddress(Network), new NBitcoin.OpenAsset.AssetMoney(
                                    new NBitcoin.OpenAsset.AssetId(new NBitcoin.OpenAsset.BitcoinAssetId(assetId, Network)),
                                    //   (long) (data.Amount * assetMultiplicationFactor))) // if data.Amount * assetMultiplicationFactor is 43 the (long)() returns 43
                                    Convert.ToInt64(data.Amount * assetMultiplicationFactor)))
                                .SendFees(new Money(OpenAssetsHelper.TransactionSendFeesInSatoshi))
                                .SetChange(assetAddress)
                                .BuildTransaction(true);

                            Error localerror = await OpenAssetsHelper.CheckTransactionForDoubleSpentThenSendIt
                                (tx, Username, Password, IpAddress, Network);

                            if (localerror == null)
                            {
                                result = new CashInTaskResult
                                {
                                    TransactionHex = tx.ToHex(),
                                    TransactionHash = tx.GetHash().ToString()
                                };
                            }
                            else
                            {
                                error = localerror;
                            }

                            
                            // Checking if the inputs has been already spent
                            // ToDo - Performance should be revisted by possible join operation
                            foreach (var item in tx.Inputs)
                            {
                                string prevOut = item.PrevOut.Hash.ToString();
                                var spentTx = await (from uxto in entitiesContext.SpentOutputs
                                                     join dbTx in entitiesContext.SentTransactions on uxto.SentTransactionId equals dbTx.id
                                                     where uxto.PrevHash.Equals(prevOut) && uxto.OutputNumber.Equals(item.PrevOut.N)
                                                     select dbTx.TransactionHex).FirstOrDefaultAsync();

                                if (spentTx != null)
                                {
                                    error = new Error();
                                    error.Code = ErrorCode.PossibleDoubleSpend;
                                    error.Message = "The output number " + item.PrevOut.N + " from transaction " + item.PrevOut.Hash +
                                        " has been already spent in transcation " + spentTx;
                                    break;
                                }
                            }

                            if (error == null)
                            {
                                // First broadcating the transaction
                                RPCClient client = new RPCClient(new System.Net.NetworkCredential(Username, Password),
                                    IpAddress, Network);
                                await client.SendRawTransactionAsync(tx);

                                // Then marking the inputs as spent
                                using (var dbTransaction = entitiesContext.Database.BeginTransaction())
                                {
                                    SentTransaction dbSentTransaction = new SentTransaction
                                    {
                                        TransactionHex = tx.ToHex()
                                    };
                                    entitiesContext.SentTransactions.Add(dbSentTransaction);
                                    await entitiesContext.SaveChangesAsync();

                                    foreach (var item in tx.Inputs)
                                    {
                                        entitiesContext.SpentOutputs.Add(new SpentOutput
                                        {
                                            OutputNumber = item.PrevOut.N,
                                            PrevHash = item.PrevOut.Hash.ToString(),
                                            SentTransactionId = dbSentTransaction.id
                                        });
                                    }
                                    await entitiesContext.SaveChangesAsync();

                                    dbTransaction.Commit();
                                }

                                result = new CashInTaskResult
                                {
                                    TransactionHex = tx.ToHex(),
                                    TransactionHash = tx.GetHash().ToString()
                                };
                            }
                            
                        }

                    }
                }
                */
            }
            catch (Exception e)
            {
                error = new Error();
                error.Code = ErrorCode.Exception;
                error.Message = e.ToString();
            }
            return new Tuple<CashInTaskResult, Error>(result, error);
        }

        public void Execute(TaskToDoCashIn data, Func<Tuple<CashInTaskResult, Error>, Task> invokeResult)
        {
            Task.Run(async () =>
            {
                var result = await ExecuteTask(data);
                await invokeResult(result);
            });
        }
    }
}

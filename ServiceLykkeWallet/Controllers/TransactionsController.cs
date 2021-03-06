﻿using Core;
using LykkeWalletServices;
using LykkeWalletServices.Accounts;
using NBitcoin;
using NBitcoin.OpenAsset;
using NBitcoin.Policy;
using NBitcoin.RPC;
using Newtonsoft.Json;
using ServiceLykkeWallet.Models;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;
using System.Web.Mvc;

namespace ServiceLykkeWallet.Controllers
{
    public class TransactionsController : ApiController
    {
        public class UnsignedTransaction
        {
            public long Id
            {
                get;
                set;
            }

            public string TransactionHex
            {
                get;
                set;
            }
        }

        public class TransferRequest
        {
            public string SourceAddress
            {
                get;
                set;
            }

            public string DestinationAddress
            {
                get;
                set;
            }

            public double Amount
            {
                get;
                set;
            }

            public string Asset
            {
                get;
                set;
            }
        }

        public class TranctionSignAndBroadcastRequest
        {
            public long Id
            {
                get;
                set;
            }

            public string ClientSignedTransaction
            {
                get;
                set;
            }
        }

        public class TransactionSignRequest
        {
            public string TransactionToSign
            {
                get;
                set;
            }

            public string PrivateKey
            {
                get;
                set;
            }
        }

        public class TransactionSignResponse
        {
            public string SignedTransaction
            {
                get;
                set;
            }
        }

        // This should respond to curl -H "Content-Type: application/json" -X POST -d "{\"SourceAddress\":\"xyz\",\"DestinationAddress\":\"xyz\", \"Amount\":10.25, \"Asset\":\"TestExchangeUSD\"}" http://localhost:8989/Transactions/CreateUnsignedTransfer
        [System.Web.Http.HttpPost]
        public async Task<IHttpActionResult> CreateUnsignedTransfer(TransferRequest transferRequest)
        {
            IHttpActionResult result = null;

            try
            {
                var ret = await CreateUnsignedTransferWorker(transferRequest);
                if (ret.Item2 == null)
                {
                    result = Json(ret.Item1);
                }
                else
                {
                    result = InternalServerError(new Exception(ret.Item2.ToString()));
                }
            }
            catch (Exception e)
            {
                result = InternalServerError(e);
            }

            await OpenAssetsHelper.SendPendingEmailsAndLogInputOutput
                (WebSettings.ConnectionString, "Transfer:" + JsonConvert.SerializeObject(transferRequest), ConvertResultToString(result));
            return result;
        }

        public async Task<string> SignTransactionWorker(TransactionSignRequest signRequest)
        {
            Transaction tx = new Transaction(signRequest.TransactionToSign);
            Transaction outputTx = new Transaction(signRequest.TransactionToSign);
            var secret = new BitcoinSecret(signRequest.PrivateKey);

            TransactionBuilder builder = new TransactionBuilder();
            tx = builder.ContinueToBuild(tx).AddKeys(new BitcoinSecret[] { secret }).SignTransaction(tx);

            for (int i = 0; i < tx.Inputs.Count; i++)
            {
                var input = tx.Inputs[i];
                var txResponse = await OpenAssetsHelper.GetTransactionHex(input.PrevOut.Hash.ToString(), WebSettings.ConnectionParams);
                if (txResponse.Item1)
                {
                    throw new Exception(string.Format("Error while retrieving transaction {0}, error is: {1}",
                        input.PrevOut.Hash.ToString(), txResponse.Item2));
                }

                ///var builder = new TransactionBuilder();

                var prevTransaction = new Transaction(txResponse.Item3);
                var output = prevTransaction.Outputs[input.PrevOut.N];
                if (PayToScriptHashTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                {
                    var redeemScript = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(input.ScriptSig).RedeemScript;
                    if (PayToMultiSigTemplate.Instance.CheckScriptPubKey(redeemScript))
                    {
                        var pubkeys = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(redeemScript).PubKeys;
                        for (int j = 0; j < pubkeys.Length; j++)
                        {
                            if (secret.PubKey.ToHex() == pubkeys[j].ToHex())
                            {
                                var scriptParams = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(input.ScriptSig);
                                var hash = Script.SignatureHash(scriptParams.RedeemScript, tx, i, SigHash.All);
                                var signature = secret.PrivateKey.Sign(hash,SigHash.All);
                                scriptParams.Pushes[j + 1] = signature.Signature.ToDER().Concat(new byte[] { 0x01 }).ToArray();
                                outputTx.Inputs[i].ScriptSig = PayToScriptHashTemplate.Instance.GenerateScriptSig(scriptParams);
                            }
                        }
                    }
                    continue;
                }

                if(PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                {
                    var address = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(output.ScriptPubKey).GetAddress(WebSettings.ConnectionParams.BitcoinNetwork).ToWif();
                    if(address == secret.GetAddress().ToWif())
                    {
                        var hash = Script.SignatureHash(output.ScriptPubKey, tx, i, SigHash.All);
                        var signature = secret.PrivateKey.Sign(hash, SigHash.All);

                        outputTx.Inputs[i].ScriptSig = PayToPubkeyHashTemplate.Instance.GenerateScriptSig(signature, secret.PubKey);
                    }

                    continue;
                }
            }

            return outputTx.ToHex();
        }

        // This should respond to curl -H "Content-Type: application/json" -X POST -d "{\"Id\":4371,\"ClientSignedTransaction\":\"xxx\"}" http://localhost:8989/Transactions/SignTransactionIfRequiredAndBroadcast
        [System.Web.Http.HttpPost]
        public async Task<IHttpActionResult> SignTransactionIfRequiredAndBroadcast(TranctionSignAndBroadcastRequest signBroadcastRequest)
        {
            Transaction finalTransaction = null;

            if (signBroadcastRequest.Id < 0)
            {
                return BadRequest("Id of the transaction should not be negative.");
            }

            try
            {
                using (SqlexpressLykkeEntities entities = new SqlexpressLykkeEntities(WebSettings.ConnectionString))
                {
                    var txRecord = (from transaction in entities.SentTransactions
                                    where transaction.id == (int)signBroadcastRequest.Id
                                    select transaction).FirstOrDefault();

                    if (txRecord == null)
                    {
                        return BadRequest(string.Format("The request transaction with id:{0} was not found.", signBroadcastRequest.Id));
                    }

                    if (txRecord.TransactionSendingSuccessful ?? false)
                    {
                        string txId = null;
                        if (!string.IsNullOrEmpty(txRecord.ExchangeSignedTransactionAfterClient))
                        {
                            txId = (new Transaction(txRecord.ExchangeSignedTransactionAfterClient)).GetHash().ToString();
                        }
                        else
                        {
                            txId = (new Transaction(txRecord.ClientSignedTransaction)).GetHash().ToString();
                        }
                        return BadRequest(string.Format("This transaction has been successfully sent before with Bitcoin transaction id: {0} .", txId));
                    }

                    if (!(txRecord.IsClientSignatureRequired ?? false))
                    {
                        return BadRequest(string.Format("The requested transaction with id:{0} does not require client side signature.", signBroadcastRequest.Id));
                    }

                    txRecord.ClientSignedTransaction = signBroadcastRequest.ClientSignedTransaction;
                    finalTransaction = new Transaction(signBroadcastRequest.ClientSignedTransaction);
                    await entities.SaveChangesAsync();

                    if (txRecord.IsExchangeSignatureRequired ?? false)
                    {
                        TransactionSignRequest request = new TransactionSignRequest
                        {
                            TransactionToSign = txRecord.TransactionHex,
                            PrivateKey = WebSettings.ExchangePrivateKey
                        };

                        var exchangeSignResult = await SignTransactionWorker(request);

                        var unsignedTx = new Transaction(txRecord.TransactionHex);
                        var clientSignedTx = new Transaction(signBroadcastRequest.ClientSignedTransaction);
                        var exchangeSignedTx = new Transaction(exchangeSignResult);
                        /*
                        TransactionBuilder builder = new TransactionBuilder();
                        finalTransaction = builder.ContinueToBuild(new Transaction(txRecord.TransactionHex)).CombineSignatures(new Transaction[] { new Transaction(txRecord.TransactionHex),
                            new Transaction(signBroadcastRequest.ClientSignedTransaction),
                            new Transaction(exchangeSignResult) });
                            */

                        for (int i = 0; i < unsignedTx.Inputs.Count; i++)
                        {
                            var input = unsignedTx.Inputs[i];
                            var txResponse = await OpenAssetsHelper.GetTransactionHex(input.PrevOut.Hash.ToString(), WebSettings.ConnectionParams);
                            if (txResponse.Item1)
                            {
                                throw new Exception(string.Format("Error while retrieving transaction {0}, error is: {1}",
                                    input.PrevOut.Hash.ToString(), txResponse.Item2));
                            }

                            var prevTransaction = new Transaction(txResponse.Item3);
                            var output = prevTransaction.Outputs[input.PrevOut.N];
                            if (PayToScriptHashTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                            {
                                var redeemScript = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(input.ScriptSig).RedeemScript;
                                if (PayToMultiSigTemplate.Instance.CheckScriptPubKey(redeemScript))
                                {
                                    var scriptParams = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(input.ScriptSig);

                                    var clientPushes = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(clientSignedTx.Inputs[i].ScriptSig).Pushes;
                                    var exchangePushes = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(exchangeSignedTx.Inputs[i].ScriptSig).Pushes;

                                    //scriptParams.Pushes = new byte[][] { clientPushes[1] , exchangePushes[2] };


                                    scriptParams.Pushes[1] = clientPushes[1];
                                    scriptParams.Pushes[2] = exchangePushes[2];


                                    finalTransaction.Inputs[i].ScriptSig = PayToScriptHashTemplate.Instance.GenerateScriptSig(scriptParams);
                                    /*
                                    var pubkeys = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(redeemScript).PubKeys;
                                    for (int j = 0; j < pubkeys.Length; j++)
                                    {
                                        if (secret.PubKey.ToHex() == pubkeys[j].ToHex())
                                        {
                                            var scriptParams = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(input.ScriptSig);
                                            scriptParams.Pushes[j + 1] = tx.SignInput(secret, new Coin(prevTransaction, output)).Signature.ToDER();
                                            outputTx.Inputs[i].ScriptSig = PayToScriptHashTemplate.Instance.GenerateScriptSig(scriptParams);
                                        }
                                    }
                                    */
                                }
                            }
                        }

                        TransactionBuilder builder = new TransactionBuilder();
                        for (int i = 0; i < finalTransaction.Inputs.Count; i++)
                        {
                            var input = finalTransaction.Inputs[i];
                            var txResponse = await OpenAssetsHelper.GetTransactionHex(input.PrevOut.Hash.ToString(), WebSettings.ConnectionParams);
                            if (txResponse.Item1)
                            {
                                throw new Exception(string.Format("Error while retrieving transaction {0}, error is: {1}",
                                    input.PrevOut.Hash.ToString(), txResponse.Item2));
                            }

                            builder.AddCoins(new Transaction(txResponse.Item3));
                        }

                        //MinerTransactionPolicy.Instance.
                        var verified = builder.Verify(finalTransaction);

                        txRecord.ExchangeSignedTransactionAfterClient = finalTransaction.ToHex();
                        await entities.SaveChangesAsync();
                    }

                    try
                    {
                        RPCClient client = new RPCClient(new System.Net.NetworkCredential(WebSettings.ConnectionParams.Username, WebSettings.ConnectionParams.Password),
                            WebSettings.ConnectionParams.IpAddress, WebSettings.ConnectionParams.BitcoinNetwork);

                        await client.SendRawTransactionAsync(finalTransaction);

                        txRecord.TransactionId = finalTransaction.GetHash().ToString();
                        txRecord.TransactionSendingSuccessful = true;
                        await entities.SaveChangesAsync();

                        return Ok(finalTransaction.GetHash().ToString());
                    }
                    catch (Exception e)
                    {
                        txRecord.TransactionSendingSuccessful = false;
                        txRecord.TransactionSendingError = e.ToString();
                        return InternalServerError(e);
                    }
                }
            }
            catch (Exception e)
            {
                return InternalServerError(e);
            }

        }

        // This should respond to curl -H "Content-Type: application/json" -X POST -d "{\"TransactionToSign\":\"xyz\",\"PrivateKey\":\"xyz\"}" http://localhost:8989/Transactions/SignTransaction
        [System.Web.Http.HttpPost]
        public async Task<IHttpActionResult> SignTransaction(TransactionSignRequest signRequest)
        {
            IHttpActionResult result = null;
            try
            {
                TransactionSignResponse response = new TransactionSignResponse
                { SignedTransaction = await SignTransactionWorker(signRequest) };
                result = Json(response);
            }
            catch (Exception e)
            {
                result = InternalServerError(e);
            }

            return result;
        }

        public string ConvertResultToString(IHttpActionResult result)
        {
            if (result is ExceptionResult)
            {
                return (result as ExceptionResult).Exception.ToString();
            }

            if (result is JsonResult<UnsignedTransaction>)
            {
                return JsonConvert.SerializeObject((result as JsonResult<UnsignedTransaction>).Content);
            }

            return result.ToString();
        }

        public async Task<Tuple<UnsignedTransaction, Error>> CreateUnsignedTransferWorker(TransferRequest data)
        {
            UnsignedTransaction result = null;
            Error error = null;
            bool isClientSignatureRequired = false;
            bool isExchangeSignatureRequired = false;
            try
            {
                using (SqlexpressLykkeEntities entities = new SqlexpressLykkeEntities(WebSettings.ConnectionString))
                {
                    var sourceAddress = OpenAssetsHelper.GetBitcoinAddressFormBase58Date(data.SourceAddress);
                    if (sourceAddress == null)
                    {
                        error = new Error();
                        error.Code = ErrorCode.InvalidAddress;
                        error.Message = "Invalid source address provided";
                    }
                    else
                    {
                        var destAddress = OpenAssetsHelper.GetBitcoinAddressFormBase58Date(data.DestinationAddress);
                        if (destAddress == null)
                        {
                            error = new Error();
                            error.Code = ErrorCode.InvalidAddress;
                            error.Message = "Invalid destination address provided";
                        }
                        else
                        {
                            KeyStorage SourceMultisigAddress = null;
                            if (sourceAddress is BitcoinScriptAddress)
                            {
                                SourceMultisigAddress = await OpenAssetsHelper.GetMatchingMultisigAddress(data.SourceAddress, entities);
                            }

                            OpenAssetsHelper.GetCoinsForWalletReturnType walletCoins = null;
                            if (sourceAddress is BitcoinPubKeyAddress)
                            {
                                walletCoins = (OpenAssetsHelper.GetOrdinaryCoinsForWalletReturnType)await OpenAssetsHelper.GetCoinsForWallet(data.SourceAddress, data.Asset.GetAssetBTCAmount(data.Amount), data.Amount, data.Asset,
                                    WebSettings.Assets, WebSettings.ConnectionParams, WebSettings.ConnectionString, entities, true, false);
                            }
                            else
                            {
                                walletCoins = (OpenAssetsHelper.GetScriptCoinsForWalletReturnType)await OpenAssetsHelper.GetCoinsForWallet(data.SourceAddress, data.Asset.GetAssetBTCAmount(data.Amount), data.Amount, data.Asset,
                                    WebSettings.Assets, WebSettings.ConnectionParams, WebSettings.ConnectionString, entities, false);
                            }
                            if (walletCoins.Error != null)
                            {
                                error = walletCoins.Error;
                            }
                            else
                            {
                                using (var transaction = entities.Database.BeginTransaction())
                                {
                                    Coin[] uncoloredCoins = null;
                                    TransactionBuilder builder = new TransactionBuilder();
                                    builder
                                        .SetChange(sourceAddress, ChangeType.Colored);
                                    if (sourceAddress is BitcoinPubKeyAddress)
                                    {
                                        isClientSignatureRequired = true;
                                        isExchangeSignatureRequired = false;

                                        if (OpenAssetsHelper.IsRealAsset(data.Asset))
                                        {
                                            builder.AddCoins(((OpenAssetsHelper.GetOrdinaryCoinsForWalletReturnType)walletCoins).AssetCoins);
                                        }
                                        else
                                        {
                                            uncoloredCoins = ((OpenAssetsHelper.GetOrdinaryCoinsForWalletReturnType)walletCoins).Coins;
                                            builder.AddCoins(uncoloredCoins);
                                        }
                                    }
                                    else
                                    {
                                        isClientSignatureRequired = true;
                                        isExchangeSignatureRequired = true;

                                        if (OpenAssetsHelper.IsRealAsset(data.Asset))
                                        {
                                            builder.AddCoins(((OpenAssetsHelper.GetScriptCoinsForWalletReturnType)walletCoins).AssetScriptCoins);
                                        }
                                        else
                                        {
                                            uncoloredCoins = ((OpenAssetsHelper.GetScriptCoinsForWalletReturnType)walletCoins).ScriptCoins;
                                            builder.AddCoins(uncoloredCoins);
                                        }
                                    }

                                    if (OpenAssetsHelper.IsRealAsset(data.Asset))
                                    {
                                        builder = (await builder.SendAsset(destAddress, new AssetMoney(new AssetId(new BitcoinAssetId(walletCoins.Asset.AssetId, WebSettings.ConnectionParams.BitcoinNetwork)),
                                            Convert.ToInt64((data.Amount * walletCoins.Asset.AssetMultiplicationFactor))))
                                        .AddEnoughPaymentFee(entities, WebSettings.ConnectionParams,
                                        WebSettings.FeeAddress, 2));
                                    }
                                    else
                                    {
                                        builder.SendWithChange(destAddress,
                                            Convert.ToInt64(data.Amount * OpenAssetsHelper.BTCToSathoshiMultiplicationFactor),
                                            uncoloredCoins,
                                            sourceAddress);
                                        builder = (await builder.AddEnoughPaymentFee(entities, WebSettings.ConnectionParams,
                                            WebSettings.FeeAddress, 0));
                                    }

                                    var tx = builder.BuildTransaction(true);

                                    var txHash = tx.GetHash().ToString();

                                    OpenAssetsHelper.SentTransactionReturnValue transactionResult = null;
                                    if (!isExchangeSignatureRequired)
                                    {
                                        transactionResult = await OpenAssetsHelper.CheckTransactionForDoubleSpentClientSignatureRequired
                                        (tx, WebSettings.ConnectionParams, entities, WebSettings.ConnectionString, null, null);
                                    }
                                    else
                                    {
                                        transactionResult = await OpenAssetsHelper.CheckTransactionForDoubleSpentBothSignaturesRequired
                                            (tx, WebSettings.ConnectionParams, entities, WebSettings.ConnectionString, null, null);
                                    }

                                    if (transactionResult.Error == null)
                                    {
                                        result = new UnsignedTransaction
                                        {
                                            TransactionHex = tx.ToHex(),
                                            Id = transactionResult.SentTransactionId
                                        };
                                    }
                                    else
                                    {
                                        error = transactionResult.Error;
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
                }
            }
            catch (Exception e)
            {
                error = new Error();
                error.Code = ErrorCode.Exception;
                error.Message = e.ToString();
            }
            return new Tuple<UnsignedTransaction, Error>(result, error);
        }
    }
}

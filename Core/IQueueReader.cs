﻿using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace Core
{
    public class AssetDefinition
    {
        public string AssetId { get; set; }
        public string AssetAddress { get; set; }
        public string Name { get; set; }
        public string PrivateKey { get; set; }
        public string DefinitionUrl { get; set; }
        public int? Divisibility { get; set; }
        [JsonIgnore]
        public long MultiplyFactor
        {
            get
            {
                return (long)Math.Pow(10, Divisibility ?? 0);
            }
        }
    }

    public class TransactionToDoBase
    {
        public string TransactionId { get; set; }
    }

    public class TaskToDoGenerateNewWallet : TransactionToDoBase
    {
    }

    public class TaskToDoGenerateFeeOutputs : TaskToDoGenerateMassOutputs
    {
    }

    public class TaskToDoGenerateIssuerOutputs : TaskToDoGenerateMassOutputs
    {
        public string AssetName
        {
            get;
            set;
        }
    }

    public class TaskToDoGenerateMassOutputs : TransactionToDoBase
    {
        public string WalletAddress
        {
            get;
            set;
        }

        public string PrivateKey
        {
            get;
            set;
        }

        public double FeeAmount
        {
            get;
            set;
        }

        public uint Count
        {
            get;
            set;
        } 
    }

    public class TaskToDoGetExpiredUnclaimedRefundingTransactions : TransactionToDoBase
    {
        public string MultisigAddress { get; set; }
    }

    public class TaskToDoGenerateRefundingTransaction : TransactionToDoBase
    {
        public string MultisigAddress { get; set; }
        public uint timeoutInMinutes { get; set; }
        public string RefundAddress { get; set; }
        public bool? JustRefundTheNonRefunded { get; set; }
    }

    public class TaskToDoGetInputWalletAddresses : TransactionToDoBase
    {
        public string Asset { get; set;}
        public string MultisigAddress { get; set; }
    }

    public class TaskToDoCashIn : TransactionToDoBase
    {
        public string MultisigAddress { get; set; }
        // ToDo - At first we assume the currency is not divisable
        public double Amount { get; set; }
        public string Currency { get; set; }
    }

    public class TaskToDoGetCurrentBalance : TransactionToDoBase
    {
        public string MultisigAddress { get; set; }
        public int MinimumConfirmation { get; set; }
    }

    public class TaskToDoGetFeeOutputsStatus : TransactionToDoBase
    {
    }

    public class TaskToDoGetIssuersOutputStatus : TransactionToDoBase
    {
    }

    public class TaskToDoCashOut : TransactionToDoBase
    {
        public string MultisigAddress { get; set; }
        // ToDo - At first we assume the currency is not divisable
        public double Amount { get; set; }
        public string Currency { get; set; }
        public string PrivateKey { get; set; }
    }

    public class TaskToDoUpdateAssets : TransactionToDoBase
    {
        public AssetDefinition[] Assets
        {
            get;
            set;
        }
    }

    public class TaskToDoCashOutSeparateSignatures : TransactionToDoBase
    {
        public string MultisigAddress { get; set; }
        // ToDo - At first we assume the currency is not divisable
        public double Amount { get; set; }
        public string Currency { get; set; }
        public string PrivateKey { get; set; }
    }

    public class TaskToDoOrdinaryCashOut : TransactionToDoBase
    {
        public string MultisigAddress { get; set; }
        // ToDo - At first we assume the currency is not divisable
        public double Amount { get; set; }
        public string Currency { get; set; }
        public string PrivateKey { get; set; }
        public string PublicWallet { get; set; }
    }

    public class TaskToDoOrdinaryCashIn : TransactionToDoBase
    {
        public string MultisigAddress { get; set; }
        // ToDo - At first we assume the currency is not divisable
        public double Amount { get; set; }
        public string Currency { get; set; }
        public string PrivateKey { get; set; }
        public string PublicWallet { get; set; }
    }

    public class TaskToDoTransfer : TransactionToDoBase
    {
        public string SourceAddress { get; set; }
        public string SourcePrivateKey { get; set; }
        public string DestinationAddress { get; set; }
        // ToDo - At first we assume the currency is not divisable
        public double Amount { get; set; }
        public string Asset { get; set; }
    }

    public class TaskToDoSwap : TransactionToDoBase
    {
        public string MultisigCustomer1 { get; set; }
        public double Amount1 { get; set; }
        public string Asset1 { get; set; }
        public string MultisigCustomer2 { get; set; }
        public double Amount2 { get; set; }
        public string Asset2 { get; set; }
    }

    public class TaskToDoGetBalance : TransactionToDoBase
    {
        public string WalletAddress { get; set; }

        public string AssetID { get; set; }
    }

    public class TaskToDoGenerateExchangeTransfer : TransactionToDoBase
    {
        public string WalletAddress01 { get; set; }
        public string WalletAddress02 { get; set; }
        public string Asset01 { get; set; }
        public string Asset02 { get; set; }
        public int Amount01 { get; set; }
        public int Amount02 { get; set; }

    }

    public class TaskToDoGetTransactionToSign : TransactionToDoBase
    {
        public string WalletAddress { get; set; }
    }

    public class TaskToDoReturnSignedTransaction : TransactionToDoBase
    {
        public string ExchangeId { get; set; }
        public string WalletAddress { get; set; }
        public string SignedTransaction { get; set; }
    }

    #region type of messages our queue can read
    public class TaskToDoDepositWithdraw : TransactionToDoBase
    {
        public string ClientPublicAddress { get; set; }
        public string AssetId { get; set; }
        /// <summary>
        /// >0 - Deposit
        /// <0 - Withdraw
        /// </summary>
        public double Amount { get; set; }
    }

    public class TaskToDoSendAsset : TransactionToDoBase
    {
        public string PublicAddressFrom { get; set; }
        public string PublicAddressTo { get; set; }
        public string AssetId { get; set; }
        public double Amount { get; set; }
    }
    #endregion


    /// <summary>
    /// Interface, which reads input Queue
    /// </summary>
    public interface IQueueReader
    {
        /// <summary>
        /// Get Task or null
        /// </summary>
        /// <returns>
        ///  Instance of task or null, if there is not task to do
        /// </returns>
        Task<TransactionToDoBase> GetTaskToDo();
    }

}

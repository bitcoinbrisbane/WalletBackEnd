//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace LykkeWalletServices
{
    using System;
    using System.Collections.Generic;
    
    public partial class KeyStorage
    {
        public string WalletAddress { get; set; }
        public string WalletPrivateKey { get; set; }
        public string MultiSigAddress { get; set; }
        public string MultiSigScript { get; set; }
        public string ExchangePrivateKey { get; set; }
        public string Network { get; set; }
        public byte[] Version { get; set; }
    }
}

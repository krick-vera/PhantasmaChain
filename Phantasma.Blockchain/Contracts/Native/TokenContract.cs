﻿using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
{
    public class TokenContract : SmartContract
    {
        public override string Name => "token";

        #region FUNGIBLE TOKENS
        public void SendTokens(Address targetChain, Address from, Address to, string symbol, BigInteger amount)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(IsParentChain(targetChain) || IsChildChain(targetChain), "target must be parent or child chain");

            var otherChain = this.Runtime.Nexus.FindChainByAddress(targetChain);
            /*TODO
            var otherConsensus = (ConsensusContract)otherChain.FindContract(ContractKind.Consensus);
            Runtime.Expect(otherConsensus.IsValidReceiver(from));*/

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible token");

            if (token.IsCapped)
            {
                var sourceSupplies = this.Runtime.Chain.GetTokenSupplies(token);
                var targetSupplies = otherChain.GetTokenSupplies(token);

                if (IsParentChain(targetChain))
                {
                    Runtime.Expect(sourceSupplies.MoveToParent(amount), "source supply check failed");
                    Runtime.Expect(targetSupplies.MoveFromChild(this.Runtime.Chain, amount), "target supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(sourceSupplies.MoveToChild(this.Runtime.Chain, amount), "source supply check failed");
                    Runtime.Expect(targetSupplies.MoveFromParent(amount), "target supply check failed");
                }
            }

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Burn(balances, from, amount), "burn failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = amount, chainAddress = Runtime.Chain.Address });
            Runtime.Notify(EventKind.TokenEscrow, to, new TokenEventData() { symbol = symbol, value = amount, chainAddress = targetChain });
        }

        public void MintTokens(Address target, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(IsWitness(token.Owner), "invalid witness");

            if (token.IsCapped)
            {
                var supplies = this.Runtime.Chain.GetTokenSupplies(token);
                Runtime.Expect(supplies.Mint(amount), "minting failed");
            }

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Mint(balances, target, amount), "minting failed");

            Runtime.Notify(EventKind.TokenMint, target, new TokenEventData() { symbol = symbol, value = amount, chainAddress = this.Runtime.Chain.Address });
        }

        public void BurnTokens(Address from, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            if (token.IsCapped)
            {
                var supplies = this.Runtime.Chain.GetTokenSupplies(token);
                Runtime.Expect(supplies.Burn(amount), "minting failed");
            }

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Burn(balances, from, amount), "burning failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = amount });
        }

        public void TransferTokens(Address source, Address destination, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(source != destination, "source and destination must be different");
            Runtime.Expect(IsWitness(source), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Transfer(balances, source, destination, amount), "transfer failed");

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
        }

        public BigInteger GetBalance(Address address, string symbol)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            return balances.Get(address);
        }
        #endregion

        #region NON FUNGIBLE TOKENS
        public BigInteger[] GetTokens(Address address, string symbol)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            return ownerships.Get(address).ToArray();
        }

        public BigInteger MintToken(Address from, string symbol, byte[] data)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var tokenID = this.Runtime.Chain.CreateNFT(token, data);
            Runtime.Expect(tokenID > 0, "invalid tokenID");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Give(from, tokenID), "give token failed");

            Runtime.Notify(EventKind.TokenMint, from, new TokenEventData() { symbol = symbol, value = tokenID });
            return tokenID;
        }

        public void BurnToken(Address from, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Take(from, tokenID), "take token failed");

            Runtime.Expect(this.Runtime.Chain.DestroyNFT(token, tokenID), "destroy token failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = tokenID });
        }

        public void TransferToken(Address source, Address destination, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(source), "invalid witness");

            Runtime.Expect(source != destination, "source and destination must be different");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Take(source, tokenID), "take token failed");
            Runtime.Expect(ownerships.Give(destination, tokenID), "give token failed");

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
        }

        public void SendToken(Address targetChain, Address from, Address to, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(IsParentChain(targetChain) || IsChildChain(targetChain), "source must be parent or child chain");

            var otherChain = this.Runtime.Nexus.FindChainByAddress(targetChain);

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "must be non-fungible token");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Take(from, tokenID), "take token failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = tokenID, chainAddress = Runtime.Chain.Address });
            Runtime.Notify(EventKind.TokenEscrow, to, new TokenEventData() { symbol = symbol, value = tokenID, chainAddress = targetChain });
        }

        #endregion

    }
}
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Protocols.TestTools.StackSdk.Security.SspiLib;

namespace Microsoft.Protocols.TestTools.StackSdk.Security.Nlmp
{
    /// <summary>
    /// the protocol api of NLMP sdk, specified in Gss Api, extended from the Sspi.
    /// this class provide the Gss Api interfaces, such as Sign/Verify/Encrypt/Decrypt.
    /// it will invoke the NlmpUtility and NlmpClient to implement the features.
    /// </summary>
    public class NlmpClientSecurityContext : ClientSecurityContext
    {
        #region Fields

        /// <summary>
        /// the nlmp client to create the nlmp packet and holds the context and config.
        /// </summary>
        private NlmpClient client;

        /// <summary>
        /// the negotiate packet.
        /// in connection-oriented mode this packet is the first packet that sent by client.
        /// </summary>
        private NlmpNegotiatePacket negotiate;

        /// <summary>
        /// the current active credential.
        /// client provide a credential which is stored in the credential field of this class.
        /// when negotiate and authenticate, they need to set the credential. the current active credential is a partly
        /// copy of credential.
        /// </summary>
        private NlmpClientCredential currentActiveCredential;

        /// <summary>
        /// the credential of user to authenticate.
        /// </summary>
        private NlmpClientCredential credential;

        /// <summary>
        /// Whether to continue process.
        /// </summary>
        private bool needContinueProcessing;

        /// <summary>
        /// The token returned by Sspi.
        /// </summary>
        private byte[] token;

        /// <summary>
        /// the sequence number of client.<para/>
        /// it's used for client to sign/encrypt message<para/>
        /// In the case of connection-oriented authentication, the SeqNum parameter MUST start at 0 and is incremented
        /// by one for each message sent.
        /// </summary>
        private uint clientSequenceNumber;

        /// <summary>
        /// the sequence number of server.<para/>
        /// it's used for client to verify/decrypt message from server<para/>
        /// The receiver expects the first received message to have SeqNum equal to 0, and to be one greater for each
        /// subsequent message received.
        /// </summary>
        private uint serverSequenceNumber;

        /// <summary>
        /// Context attribute flags.
        /// </summary>
        private ClientSecurityContextAttribute contextAttribute;
        #endregion

        #region Properties

        /// <summary>
        /// the credential of user to authenticate.
        /// </summary>
        public NlmpClientCredential Credential
        {
            get
            {
                return this.credential;
            }
            set
            {
                this.credential = value;
            }
        }


        /// <summary>
        /// the context of nlmp client. set the negotiate flags.
        /// </summary>
        public NlmpClientContext Context
        {
            get
            {
                return this.client.Context;
            }
        }


        /// <summary>
        /// the version of NlmpSdk, this value must be NTLMv1 or NTLMv2.
        /// </summary>
        public NlmpVersion Version
        {
            get
            {
                return this.client.Config.Version;
            }
            set
            {
                if (this.client.Config.Version != value)
                {
                    // clear the negotiate flags of previous version
                    this.Context.ClientConfigFlags = new NegotiateTypes();

                    // update the version
                    this.client.Config.Version = value;
                }
            }
        }


        /// <summary>
        /// Whether to continue process.
        /// </summary>
        public override bool NeedContinueProcessing
        {
            get
            {
                return this.needContinueProcessing;
            }
        }


        /// <summary>
        /// The session key that generated by sdk.
        /// </summary>
        public override byte[] SessionKey
        {
            get
            {
                return this.client.Context.ExportedSessionKey;
            }
        }


        /// <summary>
        /// The token returned by Sspi.
        /// </summary>
        public override byte[] Token
        {
            get
            {
                return this.token;
            }
        }


        /// <summary>
        /// Gets or sets sequence number for Verify, Encrypt and Decrypt message.
        /// For Digest SSP, it must be 0.
        /// </summary>
        public override uint SequenceNumber
        {
            get
            {
                return this.clientSequenceNumber;
            }
            set
            {
                this.clientSequenceNumber = value;
            }
        }


        /// <summary>
        /// Package type
        /// </summary>
        public override SecurityPackageType PackageType
        {
            get
            {
                return SecurityPackageType.Ntlm;
            }
        }


        /// <summary>
        /// Queries the sizes of the structures used in the per-message functions.
        /// </summary>
        public override SecurityPackageContextSizes ContextSizes
        {
            get
            {
                SecurityPackageContextSizes size = new SecurityPackageContextSizes();

                size.MaxSignatureSize = NlmpUtility.NLMP_MAX_SIGNATURE_SIZE;
                size.SecurityTrailerSize = NlmpUtility.NLMP_SECURITY_TRAILER_SIZE;

                return size;
            }
        }


        #endregion

        #region Constructors

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="credential">the client credential contains the user information</param>
        /// <exception cref="ArgumentNullException">the previousContext must be null</exception>
        public NlmpClientSecurityContext(NlmpClientCredential credential)
        {
            this.credential = credential;

            // initialize the nlmp client information.
            this.client = new NlmpClient(new NlmpClientConfig(NlmpVersion.v2));
            this.client.Context.ClientConfigFlags |= NegotiateTypes.NTLMSSP_NEGOTIATE_NTLM;
            this.client.Context.ClientConfigFlags |= NegotiateTypes.NTLMSSP_REQUEST_TARGET;
            this.client.Context.ClientConfigFlags |= NegotiateTypes.NTLMSSP_NEGOTIATE_ALWAYS_SIGN;
            this.client.Context.ClientConfigFlags |= NegotiateTypes.NTLMSSP_NEGOTIATE_EXTENDED_SESSIONSECURITY;
            this.client.Context.ClientConfigFlags |= NegotiateTypes.NTLMSSP_NEGOTIATE_VERSION;
            this.client.Context.ClientConfigFlags |= NegotiateTypes.NTLMSSP_NEGOTIATE_128;
            this.client.Context.ClientConfigFlags |= NegotiateTypes.NTLMSSP_NEGOTIATE_56;
            this.client.Context.ClientConfigFlags |= NegotiateTypes.NTLMSSP_NEGOTIATE_UNICODE;
            this.client.Context.ClientConfigFlags |= NegotiateTypes.NTLM_NEGOTIATE_OEM;
        }

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="credential">the client credential contains the user information</param>
        /// <param name="contextAttribute">the client Context Attribute</param>
        /// <exception cref="ArgumentNullException">the previousContext must be null</exception>
        public NlmpClientSecurityContext(NlmpClientCredential credential, ClientSecurityContextAttribute contextAttribute)
            : this(credential)
        {
            this.contextAttribute = contextAttribute;
            this.Context.Integrity = contextAttribute.HasFlag(ClientSecurityContextAttribute.Integrity);
            this.Context.ReplayDetect = contextAttribute.HasFlag(ClientSecurityContextAttribute.ReplayDetect);
            this.Context.SequenceDetect = contextAttribute.HasFlag(ClientSecurityContextAttribute.SequenceDetect);
            this.Context.Confidentiality = contextAttribute.HasFlag(ClientSecurityContextAttribute.Confidentiality);
            this.Context.Datagram = contextAttribute.HasFlag(ClientSecurityContextAttribute.Datagram);
            this.Context.Identify = contextAttribute.HasFlag(ClientSecurityContextAttribute.Identify);
        }
        #endregion

        #region Gss Api

        /// <summary>
        /// Initialize the context from a token.
        /// </summary>
        /// <param name="serverToken">the token from server</param>
        public override void Initialize(byte[] serverToken)
        {
            if (serverToken == null)
            {
                this.clientSequenceNumber = 0;
                this.serverSequenceNumber = 0;

                string domainName = string.Empty;
                string workstationName = string.Empty;

                if (!NlmpUtility.IsVersionRequired(this.Context.ClientConfigFlags))
                {
                    if (NlmpUtility.IsDomainNameSupplied(this.Context.ClientConfigFlags))
                    {
                        domainName = this.Credential.DomainName;
                    }

                    if (NlmpUtility.IsWorkstationSupplied(this.Context.ClientConfigFlags))
                    {
                        workstationName = this.Credential.TargetName;
                    }
                }

                this.currentActiveCredential = new NlmpClientCredential(
                    workstationName, domainName,
                    this.Credential.AccountName, this.Credential.Password);

                this.token = GetSecurityToken();

                this.needContinueProcessing = true;
            }
            else
            {
                this.currentActiveCredential = Credential;

                this.token = GetSecurityToken(serverToken);

                this.needContinueProcessing = false;
            }
        }


        /// <summary>
        /// Encrypts Message. User decides what SecBuffers are used.
        /// </summary>
        /// <param name="securityBuffers">
        /// the security buffer array to encrypt.<para/>
        /// it can contain none or some data security buffer, that are combine to one message to encrypt.<para/>
        /// it can contain none or some token security buffer, in which the signature will be stored.
        /// </param>
        /// <exception cref="ArgumentNullException">the securityBuffers must not be null</exception>
        public override void Encrypt(params SecurityBuffer[] securityBuffers)
        {
            NlmpUtility.UpdateSealingKeyForConnectionlessMode(
                this.client.Context.ClientConfigFlags, this.client.Context.ClientHandle,
                this.client.Context.ClientSealingKey, this.clientSequenceNumber);

            NlmpUtility.GssApiEncrypt(
                  this.client.Config.Version,
                  this.Context.ClientConfigFlags,
                  this.client.Context.ClientHandle,
                  this.client.Context.ClientSigningKey,
                  ref this.clientSequenceNumber,
                  securityBuffers);
        }


        /// <summary>
        /// This takes the given SecBuffers, which are used by SSPI method DecryptMessage.
        /// </summary>
        /// <param name="securityBuffers">
        /// the security buffer array to decrypt.<para/>
        /// it can contain none or some data security buffer, that are combine to one message to decrypt.<para/>
        /// it can contain none or some token security buffer, in which the signature is stored.
        /// </param>
        /// <returns>the encrypt result, if verify, it's the verify result.</returns>
        /// <exception cref="ArgumentNullException">the securityBuffers must not be null</exception>
        public override bool Decrypt(params SecurityBuffer[] securityBuffers)
        {
            NlmpUtility.UpdateSealingKeyForConnectionlessMode(
             this.client.Context.ClientConfigFlags, this.client.Context.ServerHandle,
             this.client.Context.ServerSealingKey, this.serverSequenceNumber);

            return NlmpUtility.GssApiDecrypt(
                this.client.Config.Version,
                this.Context.ClientConfigFlags,
                this.client.Context.ServerHandle,
                this.client.Context.ServerSigningKey,
                ref this.serverSequenceNumber,
                securityBuffers);
        }


        /// <summary>
        /// Sign data according SecBuffers.
        /// </summary>
        /// <param name="securityBuffers">
        /// the security buffer array to sign.<para/>
        /// it can contain none or some data security buffer, that are combine to one message to sign.<para/>
        /// it must contain token security buffer, in which the signature will be stored.
        /// </param>
        /// <exception cref="ArgumentNullException">the securityBuffers must not be null</exception>
        /// <exception cref="ArgumentException">securityBuffers must contain signature to store signature</exception>
        public override void Sign(params SecurityBuffer[] securityBuffers)
        {
            NlmpUtility.UpdateSealingKeyForConnectionlessMode(
             this.client.Context.ClientConfigFlags, this.client.Context.ClientHandle,
             this.client.Context.ClientSealingKey, this.clientSequenceNumber);

            NlmpUtility.GssApiSign(
                this.client.Config.Version,
                this.Context.ClientConfigFlags,
                this.client.Context.ClientHandle,
                this.client.Context.ClientSigningKey,
                ref this.clientSequenceNumber,
                securityBuffers);
        }


        /// <summary>
        /// Encrypts Message. User decides what SecBuffers are used.
        /// </summary>
        /// <param name="securityBuffers">
        /// the security buffer array to verify.<para/>
        /// it can contain none or some data security buffer, that are combine to one message to verify.<para/>
        /// it must contain token security buffer, in which the signature is stored.
        /// </param>
        /// <exception cref="ArgumentNullException">the securityBuffers must not be null</exception>
        /// <exception cref="ArgumentException">securityBuffers must contain signature to verify</exception>
        public override bool Verify(params SecurityBuffer[] securityBuffers)
        {
            NlmpUtility.UpdateSealingKeyForConnectionlessMode(
             this.client.Context.ClientConfigFlags, this.client.Context.ServerHandle,
             this.client.Context.ServerSealingKey, this.serverSequenceNumber);

            return NlmpUtility.GssApiVerify(
                this.client.Config.Version,
                this.Context.ClientConfigFlags,
                this.client.Context.ServerHandle,
                this.client.Context.ServerSigningKey,
                ref this.serverSequenceNumber,
                securityBuffers);
        }


        #endregion

        #region Implicit Ntlm Api

        /// <summary>
        /// get the implicit ntlm session security token.
        /// this api is invoked by protocol need the implicit Ntlm authenticate, such as CifsSdk and the SmbSdk with
        /// none extended session security.
        /// </summary>
        /// <param name="ntlmVersion">the version for ntlm</param>
        /// <param name="domainName">the domain of user to authenticate</param>
        /// <param name="userName">the name of user to authenticate</param>
        /// <param name="password">the password of user to authenticate</param>
        /// <param name="serverTime">the time of server. server response this field in the previous packet.</param>
        /// <param name="serverChallenge">
        /// the challenge from server. server response this field in the previous packet
        /// </param>
        /// <param name="caseInsensitivePassword">
        /// output the password in insensitive format, the LmChallengeResponse</param>
        /// <param name="caseSensitivePassword">output the password in sensitive format, the NtChallengeResponse
        /// </param>
        /// <exception cref="ArgumentException">the ntlmVersion must be valid NlmpVersion value</exception>
        [SuppressMessage(
            "Microsoft.Maintainability",
            "CA1500:VariableNamesShouldNotMatchFieldNames"
            )]
        public void GetSecurityToken(
            NlmpVersion ntlmVersion,
            string domainName,
            string userName,
            string password,
            ulong serverTime,
            ulong serverChallenge,
            out byte[] caseInsensitivePassword,
            out byte[] caseSensitivePassword)
        {
            if (ntlmVersion != NlmpVersion.v1 && ntlmVersion != NlmpVersion.v2)
            {
                throw new ArgumentException(
                    string.Format("the ntlmVersion({0}) must be valid NlmpVersion value", ntlmVersion), "ntlmVersion");
            }

            caseInsensitivePassword = null;
            caseSensitivePassword = null;

            #region Prepare the TargetInfo

            byte[] targetInfo = null;

            List<AV_PAIR> pairs = new List<AV_PAIR>();
            NlmpUtility.AddAVPair(pairs, AV_PAIR_IDs.MsvAvEOL, 0x00, null);

            targetInfo = NlmpUtility.AvPairCollectionGetBytes(pairs);
            #endregion

            #region Prepare the Nlmp Negotiate Flags

            // the flags for negotiage
            NegotiateTypes nlmpFlags = NegotiateTypes.NTLMSSP_NEGOTIATE_NTLM | NegotiateTypes.NTLM_NEGOTIATE_OEM;

            #endregion

            // exported to application for the SessionKey.
            byte[] sessionBaseKey = null;

            #region Prepare the keys

            // responseKeyNT
            byte[] responseKeyNT = NlmpUtility.GetResponseKeyNt(ntlmVersion, domainName, userName, password);

            // responseKeyLM
            byte[] responseKeyLM = NlmpUtility.GetResponseKeyLm(ntlmVersion, domainName, userName, password);

            #endregion

            #region Compute Resonse

            // clientChallenge, a 8 bytes random number.
            ulong clientChallenge = BitConverter.ToUInt64(NlmpUtility.Nonce(8), 0);

            // compute response
            NlmpUtility.ComputeResponse(
                ntlmVersion, nlmpFlags, responseKeyNT, responseKeyLM, serverChallenge, clientChallenge, serverTime,
                targetInfo, out caseSensitivePassword, out caseInsensitivePassword, out sessionBaseKey);

            #endregion

        }


        #endregion

        #region Private Methods of sdk

        /// <summary>
        /// initialize the exportedSessionKey and internal keys
        /// </summary>
        /// <param name="flags">the flags of challenge</param>
        /// <param name="challenge">the challenge packet</param>
        /// <param name="responseKeyLM">the response key lm</param>
        /// <param name="lmChallengeResponse">the challenge response lm</param>
        /// <param name="encryptedRandomSessionKey">the encrypted random session key</param>
        /// <param name="exportedSessionKey">the exported session key</param>
        private void InitializeKeys(
            NegotiateTypes flags,
            NlmpChallengePacket challenge,
            byte[] responseKeyLM,
            byte[] lmChallengeResponse,
            out byte[] encryptedRandomSessionKey,
            out byte[] exportedSessionKey
            )
        {
            // keyExchangeKey
            byte[] keyExchangeKey = null;

            // get random session key
            NlmpUtility.GetEncryptedRandomSessionKey(
                this.client.Config.Version, flags, this.client.Context.SessionBaseKey, lmChallengeResponse,
                responseKeyLM, challenge.Payload.ServerChallenge, out encryptedRandomSessionKey, out keyExchangeKey,
                out exportedSessionKey);

            this.client.Context.ClientSigningKey = NlmpUtility.SignKey(flags, exportedSessionKey, "Client");
            this.client.Context.ServerSigningKey = NlmpUtility.SignKey(flags, exportedSessionKey, "Server");
            this.client.Context.ClientSealingKey = NlmpUtility.SealKey(flags, exportedSessionKey, "Client");
            this.client.Context.ServerSealingKey = NlmpUtility.SealKey(flags, exportedSessionKey, "Server");

            NlmpUtility.RC4Init(this.client.Context.ClientHandle, this.client.Context.ClientSealingKey);
            NlmpUtility.RC4Init(this.client.Context.ServerHandle, this.client.Context.ServerSealingKey);
        }


        /// <summary>
        /// initialize the response of challenge
        /// </summary>
        /// <param name="flags">the flag for challenge</param>
        /// <param name="challenge">the challenge packet</param>
        /// <param name="targetInfo">the target info of avpairs</param>
        /// <param name="responseKeyLM">the response lm key</param>
        /// <param name="lmChallengeResponse">the challenge lm response</param>
        /// <param name="ntChallengeResponse">the nt challenge response</param>
        private void InitializeChallengeResponse(
            NegotiateTypes flags,
            NlmpChallengePacket challenge,
            ICollection<AV_PAIR> targetInfo,
            out byte[] responseKeyLM,
            out byte[] lmChallengeResponse,
            out byte[] ntChallengeResponse
            )
        {
            // responseKeyNT
            byte[] responseKeyNT = NlmpUtility.GetResponseKeyNt(
                this.client.Config.Version, this.currentActiveCredential.DomainName,
                this.currentActiveCredential.AccountName, this.currentActiveCredential.Password);

            // responseKeyLM
            responseKeyLM = NlmpUtility.GetResponseKeyLm(
                this.client.Config.Version, this.currentActiveCredential.DomainName,
                this.currentActiveCredential.AccountName, this.currentActiveCredential.Password);

            // lmChallengeResponse
            lmChallengeResponse = null;

            // ntChallengeResponse
            ntChallengeResponse = null;

            ComputeResponse(
                flags, challenge.Payload.ServerChallenge, targetInfo, responseKeyNT, responseKeyLM,
                out lmChallengeResponse, out ntChallengeResponse);

            UpdateLmChallengeResponse(targetInfo, ref lmChallengeResponse);
        }

        /// <summary>
        /// initialize the target info, contains av pairs.
        /// </summary>
        /// <param name="targetInfoBytes">the bytes of target info that specified in the AV_PAIR collection</param>
        /// <returns>the initialized target info, av pairs collection</returns>
        private ICollection<AV_PAIR> InitializeTargetInfo(
            byte[] targetInfoBytes
            )
        {
            ICollection<AV_PAIR> targetInfo = NlmpUtility.BytesGetAvPairCollection(targetInfoBytes);

            InitializeMsAvFlags(targetInfo);

            InitializeRestriction(targetInfo);

            InitializeMsAvChannelBindings(targetInfo);

            InitializeMsvAvTargetName(targetInfo);

            return targetInfo;
        }


        /// <summary>
        /// initialize the MsAvFlags of target info
        /// </summary>
        /// <param name="targetInfo">the target info collection</param>
        private void InitializeMsAvFlags(
            ICollection<AV_PAIR> targetInfo
            )
        {
            // update the MsvAvFlags of targetInfo
            if (NlmpUtility.AvPairContains(targetInfo, AV_PAIR_IDs.MsvAvFlags))
            {
                uint value = NlmpUtility.BytesToSecurityUInt32(
                    NlmpUtility.AvPairGetValue(targetInfo, AV_PAIR_IDs.MsvAvFlags)
                    );
                // if AvId field set to MsvAvFlags, set the 0x02 bit to 1.
                value |= 0x02;
                byte[] newValue = NlmpUtility.SecurityUInt32GetBytes(value);

                NlmpUtility.UpdateAvPair(targetInfo, AV_PAIR_IDs.MsvAvFlags, (ushort)newValue.Length, newValue);
            }
            else
            {
                // if AvId field set to MsvAvFlags, set the 0x02 bit to 1.
                uint value = 0x02;
                byte[] newValue = NlmpUtility.SecurityUInt32GetBytes(value);
                NlmpUtility.AddAVPair(targetInfo, AV_PAIR_IDs.MsvAvFlags, (ushort)newValue.Length, newValue);
            }
        }


        /// <summary>
        /// initialize the restriction of target info
        /// </summary>
        /// <param name="targetInfo">the target info collection</param>
        private void InitializeRestriction(
            ICollection<AV_PAIR> targetInfo
            )
        {
            // update the MsAvRestrictions of targetInfo
            // MachineID (32 bytes): A 256-bit random number created at computer 
            // startup to identify the calling machine.
            Restriction_Encoding restriction = new Restriction_Encoding();
            // defines the length, in bytes, of AV_PAIR Value.
            restriction.Size = 0x30;
            restriction.MachineID = new byte[NlmpUtility.MACHINE_ID_SIZE];
            byte[] restrictionBytes = NlmpUtility.StructGetBytes(restriction);

            if (NlmpUtility.AvPairContains(targetInfo, AV_PAIR_IDs.MsAvRestrictions))
            {
                NlmpUtility.UpdateAvPair(
                    targetInfo, AV_PAIR_IDs.MsAvRestrictions, (ushort)restrictionBytes.Length, restrictionBytes);
            }
            else
            {
                NlmpUtility.AddAVPair(
                    targetInfo, AV_PAIR_IDs.MsAvRestrictions, (ushort)restrictionBytes.Length, restrictionBytes);
            }
        }


        /// <summary>
        /// compute response
        /// </summary>
        /// <param name="flags">the flags for challenge</param>
        /// <param name="serverChallenge">the server challenge</param>
        /// <param name="targetInfo">the target info contains avpairs</param>
        /// <param name="responseKeyNT">the response nt key</param>
        /// <param name="responseKeyLM">the response lm key</param>
        /// <param name="lmChallengeResponse">the challenge response lm</param>
        /// <param name="ntChallengeResponse">the challenge response nt</param>
        private void ComputeResponse(
            NegotiateTypes flags,
            ulong serverChallenge,
            ICollection<AV_PAIR> targetInfo,
            byte[] responseKeyNT,
            byte[] responseKeyLM,
            out byte[] lmChallengeResponse,
            out byte[] ntChallengeResponse
            )
        {
            // clientChallenge, a random 8 bytes.
            ulong clientChallenge = NlmpUtility.BytesToSecurityUInt64(NlmpUtility.Nonce(8));

            // time
            ulong time = 0;
            if (!NlmpUtility.IsNtlmV1(this.client.Config.Version))
            {
                time = NlmpUtility.GetTime(targetInfo);
            }

            byte[] sessionBaseKey = null;

            // compute response
            NlmpUtility.ComputeResponse(
                this.client.Config.Version, flags, responseKeyNT, responseKeyLM, serverChallenge, clientChallenge,
                time, NlmpUtility.AvPairCollectionGetBytes(targetInfo), out ntChallengeResponse,
                out lmChallengeResponse, out sessionBaseKey);

            // save key to context
            this.client.Context.SessionBaseKey = sessionBaseKey;
        }


        /// <summary>
        /// update the lm challenge response
        /// </summary>
        /// <param name="targetInfo">the challenge packet</param>
        /// <param name="lmChallengeResponse">the lm challenge response</param>
        /// <summary>
        private void UpdateLmChallengeResponse(
            ICollection<AV_PAIR> targetInfo,
            ref byte[] lmChallengeResponse
            )
        {
            // If NTLM v2 authentication is used and the CHALLENGE_MESSAGE TargetInfo field (section 2.2.1.2) has an MsvAvTimestamp present, 
            // the client SHOULD NOT send the LmChallengeResponse and SHOULD send Z(24) instead
            if (NlmpUtility.IsNtlmV2(this.client.Config.Version) && targetInfo != null && NlmpUtility.AvPairContains(targetInfo, AV_PAIR_IDs.MsvAvTimestamp))
            {
                lmChallengeResponse = NlmpUtility.Z(24);
            }
        }


        /// <summary>
        /// initialize the mic of challenge packet
        /// </summary>
        /// <param name="exportedSessionKey">the exported session key</param>
        /// <param name="targetInfo">the target info contains av pairs.</param>
        /// <param name="authenticatePacket">the authenticate packet</param>
        /// <param name="challengePacket">the challenge packet</param>
        private void InitializeChallengeMIC(
            byte[] exportedSessionKey,
            ICollection<AV_PAIR> targetInfo,
            NlmpAuthenticatePacket authenticatePacket,
            NlmpChallengePacket challengePacket
            )
        {
            if (NlmpUtility.AvPairContains(targetInfo, AV_PAIR_IDs.MsvAvTimestamp))
            {
                // update mic with security algorithm
                byte[] mic = null;
                // if connectionless, this.negotiate is null.
                mic = NlmpUtility.GetMic(exportedSessionKey, this.negotiate, challengePacket, authenticatePacket);

                // get payload of packet
                AUTHENTICATE_MESSAGE payload = authenticatePacket.Payload;

                // update mic to payload
                payload.MIC = mic;

                // update the meaningful payload to packet
                authenticatePacket.Payload = payload;
            }
        }


        /// <summary>
        /// initialize the negotiate flags
        /// </summary>
        /// <returns>the negotiate flags for authenticate</returns>
        private NegotiateTypes InitializeNegotiateFlags()
        {
            NegotiateTypes flags = this.Context.ClientConfigFlags;

            flags |= NegotiateTypes.NTLMSSP_NEGOTIATE_TARGET_INFO;

            return flags;
        }

        //If the ClientChannelBindingsUnhashed (section 3.1.1.2) is not NULL
        private void InitializeMsAvChannelBindings(
            ICollection<AV_PAIR> targetInfo
            )
        {
            // update the MsAvChannelBinding of targetInfo
            // add an AV_PAIR structure (section 2.2.2.1) and set the AvId field to MsvAvChannelBindings and the Value field to Z(16).
            var value = NlmpUtility.Z(16);
            if (NlmpUtility.AvPairContains(targetInfo, AV_PAIR_IDs.MsvChannelBindings))
            {
                NlmpUtility.UpdateAvPair(
                    targetInfo, AV_PAIR_IDs.MsvChannelBindings, (ushort)value.Length, value);
            }
            else
            {
                NlmpUtility.AddAVPair(
                    targetInfo, AV_PAIR_IDs.MsvChannelBindings, (ushort)value.Length, value);
            }
        }

        // If ClientSuppliedTargetName (section 3.1.1.2) is not NULL
        private void InitializeMsvAvTargetName(
            ICollection<AV_PAIR> targetInfo
            )
        {
            // update the MsvAvTargetName of targetInfo
            // Add an AV_PAIR structure (section 2.2.2.1) and set the AvId field to MsvAvTargetName and the Value field to ClientSuppliedTargetName without terminating NULL.
            byte[] targetName = new byte[0];
            if (!string.IsNullOrEmpty(this.credential.TargetName))
            {
                targetName = NlmpUtility.Unicode(this.credential.TargetName);
            }

            if (NlmpUtility.AvPairContains(targetInfo, AV_PAIR_IDs.MsvAvTargetName))
            {
                NlmpUtility.UpdateAvPair(
                    targetInfo, AV_PAIR_IDs.MsvAvTargetName, (ushort)targetName.Length, targetName);
            }
            else
            {
                NlmpUtility.AddAVPair(
                    targetInfo, AV_PAIR_IDs.MsvAvTargetName, (ushort)targetName.Length, targetName);
            }
        }

        #endregion

        #region Private Methods for Protocol Api

        /// <summary>
        /// get the security token
        /// </summary>
        /// <returns>the security token</returns>
        private byte[] GetSecurityToken()
        {
            if (this.client == null)
            {
                throw new InvalidOperationException("The client is null! You must initialize this field first!");
            }

            // get current version
            VERSION version = NlmpUtility.GetVersion();

            NlmpNegotiatePacket packet = this.client.CreateNegotiatePacket(
                this.Context.ClientConfigFlags, version, this.currentActiveCredential.DomainName,
                this.currentActiveCredential.TargetName);

            this.negotiate = packet;

            return packet.ToBytes();
        }


        /// <summary>
        /// get the security token,using the token from server
        /// </summary>
        /// <param name="serverToken">the token from server challenge</param>
        /// <returns>the security token</returns>
        private byte[] GetSecurityToken(
            byte[] serverToken
            )
        {
            if (this.client == null)
            {
                throw new InvalidOperationException("The client is null! You must initialize this field first!");
            }

            NegotiateTypes flags = InitializeNegotiateFlags();

            // the challenge packet from server
            NlmpChallengePacket challenge = new NlmpChallengePacket(serverToken);

            // the target info
            ICollection<AV_PAIR> targetInfo = InitializeTargetInfo(challenge.Payload.TargetInfo);

            // responseKeyLM
            byte[] responseKeyLM;

            // lmChallengeResponse
            byte[] lmChallengeResponse;

            // ntChallengeResponse
            byte[] ntChallengeResponse;

            // initiliaze the challenge response
            InitializeChallengeResponse(
                flags, challenge, targetInfo, out responseKeyLM, out lmChallengeResponse, out ntChallengeResponse);

            // encryptedRandomSessionKey
            byte[] encryptedRandomSessionKey = null;

            // exportedSessionKey
            byte[] exportedSessionKey = null;

            // initialize keys
            InitializeKeys(
                flags, challenge, responseKeyLM, lmChallengeResponse, out encryptedRandomSessionKey,
                out exportedSessionKey);

            // save the exported sessionkey
            this.client.Context.ExportedSessionKey = exportedSessionKey;

            // create challenge packet
            NlmpAuthenticatePacket packet = this.client.CreateAuthenticatePacket(
                    flags, NlmpUtility.GetVersion(), lmChallengeResponse, ntChallengeResponse,
                    this.currentActiveCredential.DomainName, this.currentActiveCredential.AccountName,
                    Environment.MachineName, encryptedRandomSessionKey);

            // initialize the mic of challenge packet
            InitializeChallengeMIC(exportedSessionKey, targetInfo, packet, challenge);

            return packet.ToBytes();
        }

        public override object QueryContextAttributes(string contextAttribute)
        {
            throw new NotImplementedException();
        }


        #endregion
    }
}

using FFmpeg.AutoGen;
using Newtonsoft.Json;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.DirectX;
using SIPSorceryMedia.External;
using SIPSorceryMedia.FFmpeg;
using SIPSorceryMedia.OpusCodec;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ControlSync.Client
{
    public static class HostPeer
    {
        private const string STUN_URL1 = "stun:stun.l.google.com:19302";
        private const string STUN_URL2 = "stun:stun1.l.google.com:19302";


        public static ScreenSource VideoSource { get; private set; }
        public static WasAPIAudioSource AudioEncoder { get; private set; }

        private static Dictionary<int, RTCPeerConnection> peerConnections;

        private static readonly string ffmpegPath = Path.Combine(Environment.CurrentDirectory, "FFMPEG");

        /// <summary>
        /// Starts the peer connection process.
        /// </summary>
        public static async void StartPeerConnection()
        {
            AudioEncoder = new WasAPIAudioSource(new OpusAudioEncoder());

            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_DEBUG, ffmpegPath);

            VideoSource = new ScreenSource(1280, 720);

            await VideoSource.StartVideo();

            peerConnections = new Dictionary<int, RTCPeerConnection>();
        }

        public static async void GenerateOffer(int toId)
        {
            ClientPg.Log($"Generating offer for {Manager.players[toId].Username}");

            var peerConnection = CreatePeerConnection(toId);

            var offerSdp = peerConnection.createOffer(null);

            await peerConnection.setLocalDescription(offerSdp);

            peerConnections.Add(toId, peerConnection);

            var offerSerialised = JsonConvert.SerializeObject(offerSdp,
                 new Newtonsoft.Json.Converters.StringEnumConverter());
            var offerBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(offerSerialised));

            ClientSend.PeerOffer(offerBase64, toId);

        }

        /// <summary>
        /// Handles the answer from the remote peer.
        /// </summary>
        /// <param name="base64Answer">The answer in base 64</param>
        public static void HandleAnswer(string base64Answer, int fromId)
        {
            if (!peerConnections.ContainsKey(fromId) || !Manager.players.ContainsKey(fromId))
                return;

            string remoteAnswer = Encoding.UTF8.GetString(Convert.FromBase64String(base64Answer));

            RTCSessionDescriptionInit answerInit = JsonConvert.DeserializeObject<RTCSessionDescriptionInit>(remoteAnswer);

            peerConnections[fromId].setRemoteDescription(answerInit);

            ClientPg.Log($"Received answer from {Manager.players[fromId].Username}");
        }

        /// <summary>
        /// Adds an ICE candidate to the peer connection.
        /// </summary>
        /// <param name="base64ICECandidate">The ICE candidate in base 64</param>
        public static void AddICECandidate(string base64ICECandidate, int fromId)
        {
            if (!peerConnections.ContainsKey(fromId) || !Manager.players.ContainsKey(fromId))
                return;


            string remoteICECandidate = Encoding.UTF8.GetString(Convert.FromBase64String(base64ICECandidate));

            RTCIceCandidateInit iceCandidateInit = JsonConvert.DeserializeObject<RTCIceCandidateInit>(remoteICECandidate);

            peerConnections[fromId].addIceCandidate(iceCandidateInit);
        }

        /// <summary>
        /// Closes the peer connection.
        /// </summary>
        public static void CloseConnection()
        {
            foreach (var peerConnection in peerConnections)
            {
                peerConnection.Value.Close("Closing peer connection because the host disconnected");
                peerConnection.Value.Dispose();
            }
            peerConnections.Clear();

            VideoSource.Dispose();
            VideoSource = null;
            AudioEncoder.CloseAudio();
            AudioEncoder = null;

            ClientSend.ClosePeerConnection();
        }

        /// <summary>
        /// Creates a new peer connection.
        /// </summary>
        /// <returns>A new peer connection</returns>
        private static RTCPeerConnection CreatePeerConnection(int myId)
        {
            RTCConfiguration config = new RTCConfiguration
            {
                // Set ICE servers.
                iceServers = new List<RTCIceServer> { new RTCIceServer { urls = STUN_URL1 }, new RTCIceServer { urls = STUN_URL2 } }
            };
            // Create a new peer connection.
            var pc = new RTCPeerConnection(config);


            VideoSource.OnVideoSourceEncodedSample += pc.SendVideo;
            AudioEncoder.OnAudioSourceEncodedSample += pc.SendAudio;

            VideoSource.RestrictFormats(format => format.Codec == VideoCodecsEnum.H264);
            AudioEncoder.RestrictFormats(format => format.Codec == AudioCodecsEnum.OPUS);

            var videoTrack = new MediaStreamTrack(VideoSource.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
            var audioTrack = new MediaStreamTrack(AudioEncoder.GetAudioSourceFormats(), MediaStreamStatusEnum.SendOnly);
            pc.addTrack(videoTrack);
            pc.addTrack(audioTrack);

            pc.OnVideoFormatsNegotiated += (sdpFormat) => VideoSource.SetVideoSourceFormat(sdpFormat.First());
            pc.OnAudioFormatsNegotiated += (sdpFormat) => AudioEncoder.SetAudioSourceFormat(sdpFormat.First());

            // Add a handler for ICE candidate events.
            // These candidates need to be sent to the remote peer.
            pc.onicecandidate += (candidate) =>
            {
                var jCandidate = candidate.toJSON();
                var base64ICECandidate = Convert.ToBase64String(Encoding.UTF8.GetBytes(jCandidate));

                ClientSend.ICECandidate(base64ICECandidate, myId);

            };

            string remoteUsername = Manager.players[myId].Username;
            // Add a handler for connection state change events.
            // This can be used to monitor the status of the WebRTC session.
            pc.onconnectionstatechange += (state) =>
            {
                ClientPg.Log($"Peer connection state with {remoteUsername} changed to {state}.");

                if (state == RTCPeerConnectionState.closed || state == RTCPeerConnectionState.disconnected)
                {
                    pc?.Dispose();
                    peerConnections.Remove(myId);
                }
            };

            pc.OnTimeout += (mediaType) => ClientPg.Log($"Timeout on media {mediaType}.");
            pc.oniceconnectionstatechange += (state) => ClientPg.Log($"ICE connection state with {remoteUsername} changed to {state}.");

            return pc;
        }
    }
}

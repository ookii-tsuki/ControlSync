using Newtonsoft.Json;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
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

        public static RTCPeerConnectionState ConnectionState => peerConnection != null ? peerConnection.connectionState : RTCPeerConnectionState.disconnected;

        public static FFmpegVideoEndPoint1 VideoEncoder { get; private set; }
        public static WasAPIAudioSource AudioEncoder { get; private set; }

        private static RTCPeerConnection peerConnection;

        private static readonly string ffmpegPath = Path.Combine(Environment.CurrentDirectory, "FFMPEG");

        /// <summary>
        /// Starts the peer connection process.
        /// </summary>
        public static async void StartPeerConnection()
        {
            Screenshare.Start();

            VideoEncoder = new FFmpegVideoEndPoint1();

            AudioEncoder = new WasAPIAudioSource(new OpusAudioEncoder());

            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_DEBUG, ffmpegPath);

            peerConnection = CreatePeerConnection();

            var offerSdp = peerConnection.createOffer(null);
            await peerConnection.setLocalDescription(offerSdp);

            var offerSerialised = JsonConvert.SerializeObject(offerSdp,
                 new Newtonsoft.Json.Converters.StringEnumConverter());
            var offerBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(offerSerialised));

            ClientSend.PeerOffer(offerBase64);

        }

        /// <summary>
        /// Handles the answer from the remote peer.
        /// </summary>
        /// <param name="base64Answer">The answer in base 64</param>
        public static void HandleAnswer(string base64Answer)
        {
            string remoteAnswer = Encoding.UTF8.GetString(Convert.FromBase64String(base64Answer));

            RTCSessionDescriptionInit answerInit = JsonConvert.DeserializeObject<RTCSessionDescriptionInit>(remoteAnswer);

            peerConnection.setRemoteDescription(answerInit);

            ClientPg.Log("Received Answer");
        }

        /// <summary>
        /// Adds an ICE candidate to the peer connection.
        /// </summary>
        /// <param name="base64ICECandidate">The ICE candidate in base 64</param>
        public static void AddICECandidate(string base64ICECandidate)
        {
            if (peerConnection == null)
                return;

            string remoteICECandidate = Encoding.UTF8.GetString(Convert.FromBase64String(base64ICECandidate));

            RTCIceCandidateInit iceCandidateInit = JsonConvert.DeserializeObject<RTCIceCandidateInit>(remoteICECandidate);

            peerConnection.addIceCandidate(iceCandidateInit);
        }

        /// <summary>
        /// Closes the peer connection.
        /// </summary>
        public static void CloseConnection()
        {
            if (peerConnection == null)
                return;

            peerConnection.close();
            peerConnection = null;
            VideoEncoder.Dispose();
            VideoEncoder = null;
            AudioEncoder.CloseAudio();
            AudioEncoder = null;
            Screenshare.Close();

            ClientSend.ClosePeerConnection();
        }

        /// <summary>
        /// Creates a new peer connection.
        /// </summary>
        /// <returns>A new peer connection</returns>
        private static RTCPeerConnection CreatePeerConnection()
        {
            RTCConfiguration config = new RTCConfiguration
            {
                // Set ICE servers.
                iceServers = new List<RTCIceServer> { new RTCIceServer { urls = STUN_URL1 }, new RTCIceServer { urls = STUN_URL2 } }
            };
            // Create a new peer connection.
            var pc = new RTCPeerConnection(config);


            VideoEncoder.OnVideoSourceEncodedSample += pc.SendVideo;
            AudioEncoder.OnAudioSourceEncodedSample += pc.SendAudio;

            VideoEncoder.RestrictFormats(format => format.Codec == VideoCodecsEnum.H264);
            AudioEncoder.RestrictFormats(format => format.Codec == AudioCodecsEnum.OPUS);

            var videoTrack = new MediaStreamTrack(VideoEncoder.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
            var audioTrack = new MediaStreamTrack(AudioEncoder.GetAudioSourceFormats(), MediaStreamStatusEnum.SendOnly);
            pc.addTrack(videoTrack);
            pc.addTrack(audioTrack);

            pc.OnVideoFormatsNegotiated += (sdpFormat) => VideoEncoder.SetVideoSourceFormat(sdpFormat.First());
            pc.OnAudioFormatsNegotiated += (sdpFormat) => AudioEncoder.SetAudioSourceFormat(sdpFormat.First());

            // Add a handler for ICE candidate events.
            // These candidates need to be sent to the remote peer.
            pc.onicecandidate += (candidate) =>
            {
                var jCandidate = candidate.toJSON();
                var base64ICECandidate = Convert.ToBase64String(Encoding.UTF8.GetBytes(jCandidate));

                for (int i = 2; i <= Manager.players.Count; i++)
                {
                    var player = Manager.players[i];
                    ClientSend.ICECandidate(base64ICECandidate, player.Id);
                }
            };


            // Add a handler for connection state change events.
            // This can be used to monitor the status of the WebRTC session.
            pc.onconnectionstatechange += (state) =>
            {
                ClientPg.Log($"Peer connection state changed to {state}.");
            };

            pc.OnTimeout += (mediaType) => ClientPg.Log($"Timeout on media {mediaType}.");
            pc.oniceconnectionstatechange += (state) => ClientPg.Log($"ICE connection state changed to {state}.");

            return pc;
        }
    }
}

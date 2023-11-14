using System;
using System.Threading;
using System.Threading.Tasks;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorcery.Media;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using SIPSorceryMedia.Encoders;
using SIPSorceryMedia.FFmpeg;

namespace ControlSync.Client
{
    public static class HostPeer
    {
        private const string STUN_URL1 = "stun:stun.l.google.com:19302";
        private const string STUN_URL2 = "stun:stun1.l.google.com:19302";
        private const string FFMPEG_PATH = @"C:\Program Files (x86)\Ffmpeg\bin";

        public static RTCPeerConnectionState ConnectionState => pc != null ? pc.connectionState : RTCPeerConnectionState.disconnected;
        
        //public static FFmpegVideoEndPoint FFmpegVideo { get; private set; }
        public static VideoEncoderEndPoint VideoEncoder { get; private set; }

        private static RTCPeerConnection pc;


        public static async void StartPeerConnection()
        {
            VideoEncoder = new VideoEncoderEndPoint();
            
            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_VERBOSE, FFMPEG_PATH);

            pc = CreatePeerConnection();

            var offerSdp = pc.createOffer(null);
            await pc.setLocalDescription(offerSdp);

            var offerSerialised = JsonConvert.SerializeObject(offerSdp,
                 new Newtonsoft.Json.Converters.StringEnumConverter());
            var offerBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(offerSerialised));

            ClientSend.PeerOffer(offerBase64);

        }

        public static void HandleAnswer(string base64Answer)
        {
            string remoteAnswer = Encoding.UTF8.GetString(Convert.FromBase64String(base64Answer));

            RTCSessionDescriptionInit answerInit = JsonConvert.DeserializeObject<RTCSessionDescriptionInit>(remoteAnswer);
            
            pc.setRemoteDescription(answerInit);

            ClientPg.Log("Received Answer");
        }

        public static void AddICECandidate(string base64ICECandidate)
        {
            if (pc == null)
                return;

            string remoteICECandidate = Encoding.UTF8.GetString(Convert.FromBase64String(base64ICECandidate));

            RTCIceCandidateInit iceCandidateInit = JsonConvert.DeserializeObject<RTCIceCandidateInit>(remoteICECandidate);

            pc.addIceCandidate(iceCandidateInit);
        }
        public static void CloseConnection()
        {
            if (pc ==  null)
                return;

            pc.close();
            pc = null;
            VideoEncoder.Dispose();
            VideoEncoder = null;

            ClientSend.ClosePeerConnection();
        }
        private static RTCPeerConnection CreatePeerConnection()
        {
            RTCConfiguration config = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer> { new RTCIceServer { urls = STUN_URL1 }, new RTCIceServer { urls = STUN_URL2 } }
            };
            // Create a new peer connection.
            var pc = new RTCPeerConnection(config);


            VideoEncoder.OnVideoSourceEncodedSample += pc.SendVideo;

            //VideoEncoder.RestrictFormats(format => format.Codec == VideoCodecsEnum.H264);

            var videoTrack = new MediaStreamTrack(VideoEncoder.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
            pc.addTrack(videoTrack);

            pc.OnVideoFormatsNegotiated += (sdpFormat) => VideoEncoder.SetVideoSourceFormat(sdpFormat.First());

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
            pc.onconnectionstatechange += (state) =>
            {
                ClientPg.Log($"Peer connection connected changed to {state}.");
                if (state == RTCPeerConnectionState.connected)
                {
                    //await audioSrc.StartAudio();
                    //await testPatternSource.StartVideo();
                }
                else if (state == RTCPeerConnectionState.closed || state == RTCPeerConnectionState.failed)
                {
                    //await audioSrc.CloseAudio();
                    //await testPatternSource.CloseVideo();
                }
            };

            return pc;
        }
    }
}

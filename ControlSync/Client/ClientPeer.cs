using Newtonsoft.Json;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SIPSorceryMedia.Encoders;
using SIPSorceryMedia.FFmpeg;
using System.Diagnostics;

namespace ControlSync.Client
{
    public static class ClientPeer
    {
        private const string STUN_URL1 = "stun:stun.l.google.com:19302";
        private const string STUN_URL2 = "stun:stun1.l.google.com:19302";
        private const string FFMPEG_PATH = @"C:\Program Files (x86)\Ffmpeg\bin";

        public static RTCPeerConnectionState ConnectionState => pc != null ? pc.connectionState : RTCPeerConnectionState.disconnected;

        private static RTCPeerConnection pc;
        private static FFmpegVideoEndPoint VideoEncoder { get; set; }


        public static async void StartPeerConnection(string base64Offer)
        {

            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_VERBOSE, FFMPEG_PATH);

            VideoEncoder = new FFmpegVideoEndPoint();

            pc = CreatePeerConnection();

            HandleOffer(base64Offer);

            var answerSdp = pc.createAnswer(null);
            await pc.setLocalDescription(answerSdp);

            var answerSerialised = JsonConvert.SerializeObject(answerSdp,
                 new Newtonsoft.Json.Converters.StringEnumConverter());
            var answerBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(answerSerialised));

            ClientSend.PeerAnswer(answerBase64);

            Manager.ShowScreen();
        }

        public static void HandleOffer(string base64Offer)
        {
            string remoteOffer = Encoding.UTF8.GetString(Convert.FromBase64String(base64Offer));

            RTCSessionDescriptionInit offerInit = JsonConvert.DeserializeObject<RTCSessionDescriptionInit>(remoteOffer);

            pc.setRemoteDescription(offerInit);

            ClientPg.Log("Received offer");
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
            if (pc == null) return;

            pc.close();
            pc = null;
            VideoEncoder.Dispose();
            VideoEncoder = null;
        }
        private static RTCPeerConnection CreatePeerConnection()
        {
            RTCConfiguration config = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer> { new RTCIceServer { urls = STUN_URL1 }, new RTCIceServer { urls = STUN_URL2 } }
            };
            // Create a new peer connection.
            var pc = new RTCPeerConnection(config);

            //VideoEncoder.RestrictFormats(format => format.Codec == VideoCodecsEnum.H264);

            var videoTrack = new MediaStreamTrack(VideoEncoder.GetVideoSourceFormats(), MediaStreamStatusEnum.RecvOnly);

            pc.addTrack(videoTrack);

            pc.OnVideoFormatsNegotiated += (sdpFormat) => VideoEncoder.SetVideoSourceFormat(sdpFormat.First());

            pc.OnVideoFrameReceived += VideoEncoder.GotVideoFrame;

            VideoEncoder.OnVideoSinkDecodedSampleFaster += (RawImage img) =>
            {
                Manager.UpdateScreenView(img.GetBuffer(), img.Width, img.Height, img.Stride);
            };

            // Add a handler for ICE candidate events.
            // These candidates need to be sent to the remote peer.
            pc.onicecandidate += (candidate) =>
            {
                var jCandidate = candidate.toJSON();
                var base64ICECandidate = Convert.ToBase64String(Encoding.UTF8.GetBytes(jCandidate));

                ClientSend.ICECandidate(base64ICECandidate, 1);
                
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

using System;
using System.Text;
using OpenSimCity.Application.Persistence;
using UnityEngine;

namespace OpenSimCity.Platform.Persistence
{
    /// <summary>
    /// Unity JSON adapter for the field-only snapshot DTO graph. Strict UTF-8 and
    /// a required header turn malformed, binary, and truncated payloads into
    /// explicit results instead of exceptions escaping to Application.
    /// </summary>
    public sealed class UnityJsonCitySnapshotPayloadCodec : ICitySnapshotPayloadCodec
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        public SnapshotPayloadVersionResult ReadFormatVersion(byte[] payload)
        {
            if (!TryReadJson(payload, out var json))
                return SnapshotPayloadVersionResult.Failure(SnapshotPayloadError.CorruptData);

            try
            {
                var envelope = JsonUtility.FromJson<SnapshotHeaderEnvelope>(json);
                if (envelope == null ||
                    envelope.Header == null ||
                    !string.Equals(
                        envelope.Header.Magic,
                        CitySnapshotFormat.Magic,
                        StringComparison.Ordinal) ||
                    envelope.Header.FormatVersion <= 0)
                {
                    return SnapshotPayloadVersionResult.Failure(SnapshotPayloadError.CorruptData);
                }

                return SnapshotPayloadVersionResult.Success(envelope.Header.FormatVersion);
            }
            catch (Exception)
            {
                return SnapshotPayloadVersionResult.Failure(SnapshotPayloadError.CorruptData);
            }
        }

        public SnapshotPayloadEncodeResult Encode(CitySnapshotDto snapshot)
        {
            if (snapshot == null)
                return SnapshotPayloadEncodeResult.Failure(SnapshotPayloadError.EncodingFailed);

            try
            {
                var json = JsonUtility.ToJson(snapshot);
                if (string.IsNullOrWhiteSpace(json))
                    return SnapshotPayloadEncodeResult.Failure(SnapshotPayloadError.EncodingFailed);
                return SnapshotPayloadEncodeResult.Success(StrictUtf8.GetBytes(json));
            }
            catch (Exception)
            {
                return SnapshotPayloadEncodeResult.Failure(SnapshotPayloadError.EncodingFailed);
            }
        }

        public SnapshotPayloadDecodeResult Decode(byte[] payload)
        {
            if (!ReadFormatVersion(payload).IsSuccess ||
                !TryReadJson(payload, out var json))
            {
                return SnapshotPayloadDecodeResult.Failure(SnapshotPayloadError.CorruptData);
            }

            try
            {
                var snapshot = JsonUtility.FromJson<CitySnapshotDto>(json);
                return snapshot == null
                    ? SnapshotPayloadDecodeResult.Failure(SnapshotPayloadError.CorruptData)
                    : SnapshotPayloadDecodeResult.Success(snapshot);
            }
            catch (Exception)
            {
                return SnapshotPayloadDecodeResult.Failure(SnapshotPayloadError.CorruptData);
            }
        }

        private static bool TryReadJson(byte[] payload, out string json)
        {
            json = null;
            if (payload == null || payload.Length == 0)
                return false;

            try
            {
                json = StrictUtf8.GetString(payload);
            }
            catch (DecoderFallbackException)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
                return false;

            var trimmed = json.Trim();
            return trimmed.Length >= 2 &&
                   trimmed[0] == '{' &&
                   trimmed[trimmed.Length - 1] == '}' &&
                   trimmed.IndexOf('\0') < 0;
        }

        [Serializable]
        private sealed class SnapshotHeaderEnvelope
        {
            public CitySnapshotHeaderDto Header;
        }
    }
}

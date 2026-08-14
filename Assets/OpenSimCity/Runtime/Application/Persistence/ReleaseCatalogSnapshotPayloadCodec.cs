using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenSimCity.Application.Verification;

namespace OpenSimCity.Application.Persistence
{
    public static class ReleaseSaveEnvelopeFormat
    {
        public const int CurrentEnvelopeVersion = 1;
        public const int CurrentContentCatalogVersion = ReleaseBuildProfile.ContentCatalogVersion;
        internal static readonly byte[] Magic = { 0x4f, 0x53, 0x43, 0x52, 0x53, 0x41, 0x56, 0x45 }; // OSCRSAVE
    }

    public sealed class ReleaseSaveEnvelopeMetadata
    {
        internal ReleaseSaveEnvelopeMetadata(int envelopeVersion, int contentCatalogVersion, ulong[] referencedDefinitionIds)
        {
            EnvelopeVersion = envelopeVersion;
            ContentCatalogVersion = contentCatalogVersion;
            ReferencedDefinitionIds = referencedDefinitionIds == null
                ? Array.Empty<ulong>()
                : (ulong[])referencedDefinitionIds.Clone();
        }

        public int EnvelopeVersion { get; }
        public int ContentCatalogVersion { get; }
        public ulong[] ReferencedDefinitionIds { get; }
    }

    /// <summary>
    /// Deterministic platform-neutral envelope around the existing snapshot payload.
    /// The inner snapshot format/migrations remain unchanged; new release saves add
    /// catalog identity and the exact stable definition IDs referenced by the DTO graph.
    /// </summary>
    public static class ReleaseSaveEnvelope
    {
        private const int HeaderBytes = 8 + 4 + 4 + 4;
        private const int TailBytes = 4;

        public static bool IsEnvelopePayload(byte[] payload)
        {
            if (payload == null || payload.Length < ReleaseSaveEnvelopeFormat.Magic.Length) return false;
            for (var index = 0; index < ReleaseSaveEnvelopeFormat.Magic.Length; index++)
                if (payload[index] != ReleaseSaveEnvelopeFormat.Magic[index]) return false;
            return true;
        }

        public static byte[] Wrap(byte[] innerPayload, int contentCatalogVersion, ulong[] referencedDefinitionIds)
        {
            if (innerPayload == null || innerPayload.Length == 0) throw new ArgumentException("An inner snapshot payload is required.", nameof(innerPayload));
            if (contentCatalogVersion <= 0) throw new ArgumentOutOfRangeException(nameof(contentCatalogVersion));
            var references = referencedDefinitionIds == null ? Array.Empty<ulong>() : (ulong[])referencedDefinitionIds.Clone();
            ValidateStrictReferences(references, nameof(referencedDefinitionIds));

            var length = checked(HeaderBytes + references.Length * 8 + TailBytes + innerPayload.Length);
            var output = new byte[length];
            var offset = 0;
            Buffer.BlockCopy(ReleaseSaveEnvelopeFormat.Magic, 0, output, offset, ReleaseSaveEnvelopeFormat.Magic.Length);
            offset += ReleaseSaveEnvelopeFormat.Magic.Length;
            WriteInt32(output, ref offset, ReleaseSaveEnvelopeFormat.CurrentEnvelopeVersion);
            WriteInt32(output, ref offset, contentCatalogVersion);
            WriteInt32(output, ref offset, references.Length);
            for (var index = 0; index < references.Length; index++) WriteUInt64(output, ref offset, references[index]);
            WriteInt32(output, ref offset, innerPayload.Length);
            Buffer.BlockCopy(innerPayload, 0, output, offset, innerPayload.Length);
            return output;
        }

        public static bool TryUnwrap(byte[] payload, out ReleaseSaveEnvelopeMetadata metadata, out byte[] innerPayload)
        {
            metadata = null;
            innerPayload = null;
            if (!IsEnvelopePayload(payload) || payload.Length < HeaderBytes + TailBytes) return false;

            try
            {
                var offset = ReleaseSaveEnvelopeFormat.Magic.Length;
                var envelopeVersion = ReadInt32(payload, ref offset);
                var contentVersion = ReadInt32(payload, ref offset);
                var referenceCount = ReadInt32(payload, ref offset);
                if (envelopeVersion <= 0 || contentVersion <= 0 || referenceCount < 0) return false;
                if (referenceCount > (payload.Length - offset - TailBytes) / 8) return false;

                var references = new ulong[referenceCount];
                for (var index = 0; index < referenceCount; index++) references[index] = ReadUInt64(payload, ref offset);
                if (!ReferencesAreStrict(references)) return false;

                var innerLength = ReadInt32(payload, ref offset);
                if (innerLength <= 0 || innerLength != payload.Length - offset) return false;
                innerPayload = new byte[innerLength];
                Buffer.BlockCopy(payload, offset, innerPayload, 0, innerLength);
                metadata = new ReleaseSaveEnvelopeMetadata(envelopeVersion, contentVersion, references);
                return true;
            }
            catch (Exception)
            {
                metadata = null;
                innerPayload = null;
                return false;
            }
        }

        private static void ValidateStrictReferences(ulong[] references, string parameterName)
        {
            if (!ReferencesAreStrict(references))
                throw new ArgumentException("Definition references must be non-zero, unique and strictly ascending.", parameterName);
        }

        private static bool ReferencesAreStrict(ulong[] references)
        {
            if (references == null) return false;
            ulong previous = 0;
            for (var index = 0; index < references.Length; index++)
            {
                var current = references[index];
                if (current == 0 || (index > 0 && current <= previous)) return false;
                previous = current;
            }
            return true;
        }

        private static void WriteInt32(byte[] buffer, ref int offset, int value)
        {
            unchecked
            {
                buffer[offset++] = (byte)value;
                buffer[offset++] = (byte)(value >> 8);
                buffer[offset++] = (byte)(value >> 16);
                buffer[offset++] = (byte)(value >> 24);
            }
        }

        private static int ReadInt32(byte[] buffer, ref int offset)
        {
            if (offset > buffer.Length - 4) throw new IndexOutOfRangeException();
            var value = buffer[offset] |
                        (buffer[offset + 1] << 8) |
                        (buffer[offset + 2] << 16) |
                        (buffer[offset + 3] << 24);
            offset += 4;
            return value;
        }

        private static void WriteUInt64(byte[] buffer, ref int offset, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8) buffer[offset++] = (byte)(value >> shift);
        }

        private static ulong ReadUInt64(byte[] buffer, ref int offset)
        {
            if (offset > buffer.Length - 8) throw new IndexOutOfRangeException();
            ulong value = 0;
            for (var shift = 0; shift < 64; shift += 8) value |= (ulong)buffer[offset++] << shift;
            return value;
        }
    }

    /// <summary>
    /// Finds every serialized ulong field whose name ends in DefinitionId. This keeps
    /// catalog references synchronized with new DTO subgraphs without enumerating instance IDs.
    /// </summary>
    public static class SnapshotDefinitionReferenceCollector
    {
        public static ulong[] Collect(CitySnapshotDto snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var values = new HashSet<ulong>();
            Visit(snapshot, values);
            var ordered = new List<ulong>(values);
            ordered.Sort();
            return ordered.ToArray();
        }

        private static void Visit(object value, ISet<ulong> references)
        {
            if (value == null) return;
            var type = value.GetType();
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum) return;
            if (value is IEnumerable sequence)
            {
                foreach (var item in sequence) Visit(item, references);
                return;
            }

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (var index = 0; index < fields.Length; index++)
            {
                var field = fields[index];
                if (field.IsStatic) continue;
                var fieldValue = field.GetValue(value);
                if (field.FieldType == typeof(ulong) && field.Name.EndsWith("DefinitionId", StringComparison.Ordinal))
                {
                    var id = (ulong)fieldValue;
                    if (id != 0) references.Add(id);
                    continue;
                }
                Visit(fieldValue, references);
            }
        }
    }

    /// <summary>
    /// Adds release catalog metadata to new saves while preserving the existing inner
    /// snapshot version dispatcher. Legacy unwrapped v1/v2 payloads remain readable.
    /// Annotated saves fail closed if catalog metadata is corrupt, unsupported or does
    /// not exactly describe the decoded snapshot references.
    /// </summary>
    public sealed class ReleaseCatalogSnapshotPayloadCodec : ICitySnapshotPayloadCodec
    {
        private readonly ICitySnapshotPayloadCodec _inner;

        public ReleaseCatalogSnapshotPayloadCodec(ICitySnapshotPayloadCodec inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public SnapshotPayloadVersionResult ReadFormatVersion(byte[] payload)
        {
            if (!TryResolveInner(payload, out var innerPayload, out _))
                return SnapshotPayloadVersionResult.Failure(SnapshotPayloadError.CorruptData);
            return _inner.ReadFormatVersion(innerPayload);
        }

        public SnapshotPayloadEncodeResult Encode(CitySnapshotDto snapshot)
        {
            if (snapshot == null) return SnapshotPayloadEncodeResult.Failure(SnapshotPayloadError.EncodingFailed);
            try
            {
                var encoded = _inner.Encode(snapshot);
                if (encoded == null || !encoded.IsSuccess || encoded.Payload == null)
                    return SnapshotPayloadEncodeResult.Failure(SnapshotPayloadError.EncodingFailed);
                var references = SnapshotDefinitionReferenceCollector.Collect(snapshot);
                var wrapped = ReleaseSaveEnvelope.Wrap(
                    encoded.Payload,
                    ReleaseSaveEnvelopeFormat.CurrentContentCatalogVersion,
                    references);
                return SnapshotPayloadEncodeResult.Success(wrapped);
            }
            catch (Exception)
            {
                return SnapshotPayloadEncodeResult.Failure(SnapshotPayloadError.EncodingFailed);
            }
        }

        public SnapshotPayloadDecodeResult Decode(byte[] payload)
        {
            if (!TryResolveInner(payload, out var innerPayload, out var metadata))
                return SnapshotPayloadDecodeResult.Failure(SnapshotPayloadError.CorruptData);

            SnapshotPayloadDecodeResult decoded;
            try
            {
                decoded = _inner.Decode(innerPayload);
            }
            catch (Exception)
            {
                return SnapshotPayloadDecodeResult.Failure(SnapshotPayloadError.CorruptData);
            }
            if (decoded == null || !decoded.IsSuccess || decoded.Snapshot == null) return decoded;
            if (metadata == null) return decoded; // previous supported unwrapped save

            var actualReferences = SnapshotDefinitionReferenceCollector.Collect(decoded.Snapshot);
            if (!SameReferences(metadata.ReferencedDefinitionIds, actualReferences))
                return SnapshotPayloadDecodeResult.Failure(SnapshotPayloadError.CorruptData);
            return decoded;
        }

        public bool TryReadReleaseMetadata(byte[] payload, out ReleaseSaveEnvelopeMetadata metadata)
        {
            metadata = null;
            if (!ReleaseSaveEnvelope.IsEnvelopePayload(payload)) return false;
            if (!ReleaseSaveEnvelope.TryUnwrap(payload, out metadata, out _)) return false;
            return metadata.EnvelopeVersion == ReleaseSaveEnvelopeFormat.CurrentEnvelopeVersion &&
                   metadata.ContentCatalogVersion == ReleaseSaveEnvelopeFormat.CurrentContentCatalogVersion;
        }

        private static bool SameReferences(ulong[] left, ulong[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
        }

        private static bool MetadataSupported(ReleaseSaveEnvelopeMetadata metadata)
        {
            return metadata != null &&
                   metadata.EnvelopeVersion == ReleaseSaveEnvelopeFormat.CurrentEnvelopeVersion &&
                   metadata.ContentCatalogVersion == ReleaseSaveEnvelopeFormat.CurrentContentCatalogVersion;
        }

        private static bool TryResolveInner(
            byte[] payload,
            out byte[] innerPayload,
            out ReleaseSaveEnvelopeMetadata metadata)
        {
            innerPayload = null;
            metadata = null;
            if (payload == null || payload.Length == 0) return false;
            if (!ReleaseSaveEnvelope.IsEnvelopePayload(payload))
            {
                innerPayload = (byte[])payload.Clone();
                return true;
            }
            if (!ReleaseSaveEnvelope.TryUnwrap(payload, out metadata, out innerPayload)) return false;
            return MetadataSupported(metadata);
        }
    }
}

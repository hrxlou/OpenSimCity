using NUnit.Framework;
using OpenSimCity.Application.Persistence;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class ReleaseSaveCatalogCompatibilityTests
    {
        [Test]
        public void NewSaveDeclaresSortedDefinitionReferencesAndCatalogVersion()
        {
            var snapshot = Snapshot();
            var codec = new ReleaseCatalogSnapshotPayloadCodec(new FakeCodec(snapshot));

            var encoded = codec.Encode(snapshot);

            Assert.That(encoded.IsSuccess, Is.True);
            Assert.That(codec.TryReadReleaseMetadata(encoded.Payload, out var metadata), Is.True);
            Assert.That(metadata.ContentCatalogVersion, Is.EqualTo(ReleaseSaveEnvelopeFormat.CurrentContentCatalogVersion));
            Assert.That(metadata.ReferencedDefinitionIds, Is.EqualTo(new ulong[] { 10, 20, 30 }));
        }

        [Test]
        public void SameSnapshotProducesSameEnvelopeBytes()
        {
            var snapshot = Snapshot();
            var codec = new ReleaseCatalogSnapshotPayloadCodec(new FakeCodec(snapshot));

            var first = codec.Encode(snapshot);
            var second = codec.Encode(snapshot);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Payload, Is.EqualTo(first.Payload));
        }

        [Test]
        public void MetadataReferenceMismatchFailsClosed()
        {
            var snapshot = Snapshot();
            var inner = new FakeCodec(snapshot);
            var codec = new ReleaseCatalogSnapshotPayloadCodec(inner);
            var wrapped = ReleaseSaveEnvelope.Wrap(
                inner.Encode(snapshot).Payload,
                ReleaseSaveEnvelopeFormat.CurrentContentCatalogVersion,
                new ulong[] { 10, 20, 999 });

            var decoded = codec.Decode(wrapped);

            Assert.That(decoded.IsSuccess, Is.False);
            Assert.That(decoded.Error, Is.EqualTo(SnapshotPayloadError.CorruptData));
        }

        [Test]
        public void UnknownCatalogVersionFailsClosed()
        {
            var snapshot = Snapshot();
            var inner = new FakeCodec(snapshot);
            var codec = new ReleaseCatalogSnapshotPayloadCodec(inner);
            var wrapped = ReleaseSaveEnvelope.Wrap(
                inner.Encode(snapshot).Payload,
                ReleaseSaveEnvelopeFormat.CurrentContentCatalogVersion + 1,
                SnapshotDefinitionReferenceCollector.Collect(snapshot));

            Assert.That(codec.Decode(wrapped).IsSuccess, Is.False);
            Assert.That(codec.ReadFormatVersion(wrapped).IsSuccess, Is.False);
        }

        [Test]
        public void PreviousUnwrappedSnapshotPayloadRemainsReadable()
        {
            var snapshot = Snapshot();
            var inner = new FakeCodec(snapshot);
            var codec = new ReleaseCatalogSnapshotPayloadCodec(inner);
            var legacyPayload = inner.Encode(snapshot).Payload;

            var version = codec.ReadFormatVersion(legacyPayload);
            var decoded = codec.Decode(legacyPayload);

            Assert.That(version.IsSuccess, Is.True);
            Assert.That(version.FormatVersion, Is.EqualTo(CitySnapshotFormat.CurrentVersion));
            Assert.That(decoded.IsSuccess, Is.True);
            Assert.That(decoded.Snapshot, Is.SameAs(snapshot));
        }

        [Test]
        public void DuplicateOrUnsortedEnvelopeReferencesAreRejectedAtCreation()
        {
            Assert.That(() => ReleaseSaveEnvelope.Wrap(
                    new byte[] { 1 },
                    ReleaseSaveEnvelopeFormat.CurrentContentCatalogVersion,
                    new ulong[] { 20, 10 }),
                Throws.ArgumentException);
            Assert.That(() => ReleaseSaveEnvelope.Wrap(
                    new byte[] { 1 },
                    ReleaseSaveEnvelopeFormat.CurrentContentCatalogVersion,
                    new ulong[] { 10, 10 }),
                Throws.ArgumentException);
        }

        private static CitySnapshotDto Snapshot()
        {
            return new CitySnapshotDto
            {
                Header = new CitySnapshotHeaderDto
                {
                    FormatVersion = CitySnapshotFormat.CurrentVersion,
                    DefaultRoadDefinitionId = 20,
                    DefaultPowerPlantDefinitionId = 10
                },
                Roads = new RoadNetworkSnapshotDto
                {
                    Cells = new[] { new RoadCellSnapshotDto { CellIndex = 1, DefinitionId = 30 } }
                }
            };
        }

        private sealed class FakeCodec : ICitySnapshotPayloadCodec
        {
            private readonly CitySnapshotDto _snapshot;
            private static readonly byte[] Payload = { 0x7b, 0x7d };

            public FakeCodec(CitySnapshotDto snapshot) => _snapshot = snapshot;

            public SnapshotPayloadVersionResult ReadFormatVersion(byte[] payload) =>
                payload == null || payload.Length == 0
                    ? SnapshotPayloadVersionResult.Failure(SnapshotPayloadError.CorruptData)
                    : SnapshotPayloadVersionResult.Success(_snapshot.Header.FormatVersion);

            public SnapshotPayloadEncodeResult Encode(CitySnapshotDto snapshot) =>
                SnapshotPayloadEncodeResult.Success(Payload);

            public SnapshotPayloadDecodeResult Decode(byte[] payload) =>
                payload == null || payload.Length == 0
                    ? SnapshotPayloadDecodeResult.Failure(SnapshotPayloadError.CorruptData)
                    : SnapshotPayloadDecodeResult.Success(_snapshot);
        }
    }
}

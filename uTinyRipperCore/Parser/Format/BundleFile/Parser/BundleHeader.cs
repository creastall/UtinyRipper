﻿using System;
using System.Collections.Generic;

namespace uTinyRipper.BundleFiles
{
	public sealed class BundleHeader
	{
		public static BundleType ParseSignature(string signature)
		{
			if (TryParseSignature(signature, out BundleType bundleType))
			{
				return bundleType;
			}
			throw new ArgumentException($"Unsupported signature '{signature}'");
		}

		public static bool TryParseSignature(string signatureString, out BundleType type)
		{
			switch (signatureString)
			{
				case nameof(BundleType.UnityWeb):
					type = BundleType.UnityWeb;
					return true;

				case nameof(BundleType.UnityRaw):
					type = BundleType.UnityRaw;
					return true;

				case nameof(BundleType.UnityFS):
					type = BundleType.UnityFS;
					return true;

				case HexFASignature:
					type = BundleType.HexFA;
					return true;

				default:
					type = default;
					return false;
			}
		}

		/// <summary>
		/// 2.6.0 and greater
		/// </summary>
		public static bool IsReadBundleSize(BundleGeneration generation)
		{
			return generation >= BundleGeneration.BF_260_340;
		}
		/// <summary>
		/// 3.5.0 and greater
		/// </summary>
		public static bool IsReadMetadataDecompressedSize(BundleGeneration generation)
		{
			return generation >= BundleGeneration.BF_350_4x;
		}

		public void Read(EndianReader reader)
		{
			string signature = reader.ReadStringZeroTerm();
			Type = ParseSignature(signature);

			Generation = (BundleGeneration)reader.ReadInt32();

			PlayerVersion = reader.ReadStringZeroTerm();
			string engineVersion = reader.ReadStringZeroTerm();
			EngineVersion.Parse(engineVersion);

			switch (Type)
			{
				case BundleType.UnityRaw:
				case BundleType.UnityWeb:
				case BundleType.HexFA:
					ReadRawWeb(reader);
					break;

				case BundleType.UnityFS:
					ReadFileStream(reader);
					break;

				default:
					throw new Exception($"Unknown bundle signature '{Type}'");
			}

		}

		private void ReadRawWeb(EndianReader reader)
		{
			if (Generation < BundleGeneration.BF_530_x)
			{
				ReadPre530Generation(reader);
			}
			else
			{
				Read530Generation(reader);
				reader.BaseStream.Position++;
			}
		}

		private void ReadFileStream(EndianReader reader)
		{
			if (Generation < BundleGeneration.BF_530_x)
			{
				throw new NotSupportedException("File stream supports only 530 and greater generations");
			}

			Read530Generation(reader);
		}

		private void ReadPre530Generation(EndianReader reader)
		{
			MinimumStreamedBytes = reader.ReadUInt32();
			HeaderSize = reader.ReadInt32();
			TotalChunkCount = reader.ReadInt32();
			m_chunkInfos = reader.ReadArray<ChunkInfo>();
			if (IsReadBundleSize(Generation))
			{
				BundleSize = reader.ReadUInt32();
			}
			if (IsReadMetadataDecompressedSize(Generation))
			{
				MetadataDecompressedSize = (int)reader.ReadUInt32();
			}
			reader.BaseStream.Position++;
		}

		private void Read530Generation(EndianReader reader)
		{
			BundleSize = reader.ReadInt64();
			MetadataCompressedSize = reader.ReadInt32();
			MetadataDecompressedSize = reader.ReadInt32();
			Flags = (BundleFlag)reader.ReadInt32();
		}

		/// <summary>
		/// Signature
		/// </summary>
		public BundleType Type { get; private set; }
		/// <summary>
		/// Stream version
		/// </summary>
		public BundleGeneration Generation { get; private set; }
		/// <summary>
		/// Engine version
		/// </summary>
		public string PlayerVersion { get; private set; }

		/// <summary>
		/// Minimum number of bytes to read for streamed bundles, equal to BundleSize for normal bundles
		/// </summary>
		internal uint MinimumStreamedBytes { get; private set; }
		/// <summary>
		/// Equal to 1 if it's a streamed bundle, number of LZMAChunkInfos + mainData assets otherwise
		/// </summary>
		internal int TotalChunkCount { get; private set; }
		/// <summary>
		/// LZMA chunks info
		/// </summary>
		internal IReadOnlyList<ChunkInfo> ChunkInfos => m_chunkInfos;
		/// <summary>
		/// Size of the header
		/// </summary>
		public int HeaderSize { get; private set; }

		/// <summary>
		/// Equal to file size, sometimes equal to uncompressed data size without the header
		/// </summary>
		public long BundleSize { get; private set; }
		/// <summary>
		/// UnityFS length of the possibly-compressed (LZMA, LZ4) bundle data header
		/// </summary>
		public int MetadataCompressedSize { get; private set; }
		/// <summary>
		/// Decompressed size
		/// </summary>
		public int MetadataDecompressedSize { get; private set; }
		/// <summary>
		/// UnityFS flags
		/// </summary>
		internal BundleFlag Flags { get; private set; }

		/// <summary>
		/// Minimum revision
		/// </summary>
		public Version EngineVersion;

		private const string HexFASignature = "\xFA\xFA\xFA\xFA\xFA\xFA\xFA\xFA";

		private ChunkInfo[] m_chunkInfos;
	}
}

// Copyright © 2026 jjopensoftworks-blip

#pragma warning disable SKEXP0001 // Suppress Semantic Kernel Experimental API warnings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;
using Microsoft.Extensions.VectorData;

namespace Scrubkit
{
    /// <summary>
    /// Extension methods for registering Scrubkit document ingestion with Semantic Kernel.
    /// </summary>
    public static class ScrubkitSemanticKernelExtensions
    {
        /// <summary>
        /// Saves a collection of <see cref="Chunk"/> records to the semantic text memory.
        /// </summary>
        /// <param name="memory">The semantic text memory instance.</param>
        /// <param name="collection">The name of the collection to save the chunks in.</param>
        /// <param name="chunks">The chunks to save.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of unique IDs generated for the saved chunks.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="memory"/>, <paramref name="collection"/>, or <paramref name="chunks"/> is null.</exception>
        public static async Task<IReadOnlyList<string>> SaveChunksAsync(
            this ISemanticTextMemory memory,
            string collection,
            IEnumerable<Chunk> chunks,
            CancellationToken cancellationToken = default)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));

            var ids = new List<string>();
            foreach (var chunk in chunks)
            {
                var chunkId = $"{chunk.Path}#chunk{chunk.Index}";
                var metadataJson = System.Text.Json.JsonSerializer.Serialize(chunk.Metadata);

                var savedId = await memory.SaveInformationAsync(
                    collection: collection,
                    text: chunk.Text,
                    id: chunkId,
                    description: chunk.Name,
                    additionalMetadata: metadataJson,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                ids.Add(savedId);
            }
            return ids;
        }

        /// <summary>
        /// Saves an asynchronous stream of <see cref="Chunk"/> records to the semantic text memory.
        /// </summary>
        /// <param name="memory">The semantic text memory instance.</param>
        /// <param name="collection">The name of the collection to save the chunks in.</param>
        /// <param name="chunks">The asynchronous stream of chunks to save.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of unique IDs generated for the saved chunks.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="memory"/>, <paramref name="collection"/>, or <paramref name="chunks"/> is null.</exception>
        public static async Task<IReadOnlyList<string>> SaveChunksAsync(
            this ISemanticTextMemory memory,
            string collection,
            IAsyncEnumerable<Chunk> chunks,
            CancellationToken cancellationToken = default)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));

            var ids = new List<string>();
            await foreach (var chunk in System.Threading.Tasks.TaskAsyncEnumerableExtensions.WithCancellation<Chunk>(chunks, cancellationToken).ConfigureAwait(false))
            {
                var chunkId = $"{chunk.Path}#chunk{chunk.Index}";
                var metadataJson = System.Text.Json.JsonSerializer.Serialize(chunk.Metadata);

                var savedId = await memory.SaveInformationAsync(
                    collection: collection,
                    text: chunk.Text,
                    id: chunkId,
                    description: chunk.Name,
                    additionalMetadata: metadataJson,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                ids.Add(savedId);
            }
            return ids;
        }

        /// <summary>
        /// Scans a local directory, extracts text, redacts sensitive information, chunks it, and imports it directly into the semantic text memory.
        /// </summary>
        /// <param name="memory">The semantic text memory instance.</param>
        /// <param name="collection">The name of the collection to save the chunks in.</param>
        /// <param name="scrubber">The folder scrubber instance used to parse and redact files.</param>
        /// <param name="folderPath">The path to the local folder containing files to scan.</param>
        /// <param name="chunker">The chunker instance used to split document text into overlapping windows. Uses default options if null.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of unique IDs generated for the saved chunks.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="memory"/>, <paramref name="collection"/>, or <paramref name="scrubber"/>, or <paramref name="folderPath"/> is null.</exception>
        public static async Task<IReadOnlyList<string>> ImportFolderAsync(
            this ISemanticTextMemory memory,
            string collection,
            FolderScrubber scrubber,
            string folderPath,
            Chunker? chunker = null,
            CancellationToken cancellationToken = default)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (scrubber == null) throw new ArgumentNullException(nameof(scrubber));
            if (folderPath == null) throw new ArgumentNullException(nameof(folderPath));

            var targetChunker = chunker ?? new Chunker();
            var savedIds = new List<string>();

            await foreach (var record in scrubber.ReadStreamAsync(folderPath, cancellationToken).ConfigureAwait(false))
            {
                var chunks = targetChunker.Chunk(record);
                if (chunks.Count > 0)
                {
                    var ids = await memory.SaveChunksAsync(collection, chunks, cancellationToken).ConfigureAwait(false);
                    savedIds.AddRange(ids);
                }
            }

            return savedIds;
        }

        /// <summary>
        /// Upserts a collection of <see cref="Chunk"/> records into a vector store record collection.
        /// </summary>
        /// <typeparam name="TKey">The type of the key in the vector store collection.</typeparam>
        /// <typeparam name="TRecord">The record schema type used by the vector collection.</typeparam>
        /// <param name="collection">The vector store record collection.</param>
        /// <param name="chunks">The chunks to upsert.</param>
        /// <param name="mapper">The mapping function to transform a Scrubkit chunk into a database-specific vector store record.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of keys representing the upserted records.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="collection"/>, <paramref name="chunks"/>, or <paramref name="mapper"/> is null.</exception>
        public static async Task<IReadOnlyList<TKey>> UpsertChunksAsync<TKey, TRecord>(
            this IVectorStoreRecordCollection<TKey, TRecord> collection,
            IEnumerable<Chunk> chunks,
            Func<Chunk, TRecord> mapper,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));

            var records = chunks.Select(mapper).ToList();
            if (records.Count == 0) return Array.Empty<TKey>();

            var keys = new List<TKey>();
            await foreach (var key in collection.UpsertBatchAsync(records, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                keys.Add(key);
            }
            return keys;
        }

        /// <summary>
        /// Upserts an asynchronous stream of <see cref="Chunk"/> records into a vector store record collection.
        /// </summary>
        /// <typeparam name="TKey">The type of the key in the vector store collection.</typeparam>
        /// <typeparam name="TRecord">The record schema type used by the vector collection.</typeparam>
        /// <param name="collection">The vector store record collection.</param>
        /// <param name="chunks">The stream of chunks to upsert.</param>
        /// <param name="mapper">The mapping function to transform a Scrubkit chunk into a database-specific vector store record.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of keys representing the upserted records.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="collection"/>, <paramref name="chunks"/>, or <paramref name="mapper"/> is null.</exception>
        public static async Task<IReadOnlyList<TKey>> UpsertChunksAsync<TKey, TRecord>(
            this IVectorStoreRecordCollection<TKey, TRecord> collection,
            IAsyncEnumerable<Chunk> chunks,
            Func<Chunk, TRecord> mapper,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));

            var records = new List<TRecord>();
            await foreach (var chunk in System.Threading.Tasks.TaskAsyncEnumerableExtensions.WithCancellation<Chunk>(chunks, cancellationToken).ConfigureAwait(false))
            {
                records.Add(mapper(chunk));
            }

            if (records.Count == 0) return Array.Empty<TKey>();

            var keys = new List<TKey>();
            await foreach (var key in collection.UpsertBatchAsync(records, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                keys.Add(key);
            }
            return keys;
        }

        /// <summary>
        /// Scans a local directory, extracts text, redacts sensitive information, chunks it, maps it, and imports it directly into a vector store record collection.
        /// </summary>
        /// <typeparam name="TKey">The type of the key in the vector store collection.</typeparam>
        /// <typeparam name="TRecord">The record schema type used by the vector collection.</typeparam>
        /// <param name="collection">The vector store record collection.</param>
        /// <param name="scrubber">The folder scrubber instance used to parse and redact files.</param>
        /// <param name="folderPath">The path to the local folder containing files to scan.</param>
        /// <param name="mapper">The mapping function to transform a Scrubkit chunk into a database-specific vector store record.</param>
        /// <param name="chunker">The chunker instance used to split document text into overlapping windows. Uses default options if null.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of keys representing the upserted records.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="collection"/>, <paramref name="scrubber"/>, <paramref name="folderPath"/>, or <paramref name="mapper"/> is null.</exception>
        public static async Task<IReadOnlyList<TKey>> ImportFolderAsync<TKey, TRecord>(
            this IVectorStoreRecordCollection<TKey, TRecord> collection,
            FolderScrubber scrubber,
            string folderPath,
            Func<Chunk, TRecord> mapper,
            Chunker? chunker = null,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (scrubber == null) throw new ArgumentNullException(nameof(scrubber));
            if (folderPath == null) throw new ArgumentNullException(nameof(folderPath));
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));

            var targetChunker = chunker ?? new Chunker();
            var savedKeys = new List<TKey>();

            await foreach (var record in scrubber.ReadStreamAsync(folderPath, cancellationToken).ConfigureAwait(false))
            {
                var chunks = targetChunker.Chunk(record);
                if (chunks.Count > 0)
                {
                    var keys = await collection.UpsertChunksAsync(chunks, mapper, cancellationToken).ConfigureAwait(false);
                    savedKeys.AddRange(keys);
                }
            }

            return savedKeys;
        }
    }
}

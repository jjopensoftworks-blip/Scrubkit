#pragma warning disable SKEXP0001 // Suppress Semantic Kernel Experimental API warnings

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.Extensions.VectorData;
using Xunit;
using Scrubkit;

namespace Scrubkit.Tests
{
    public class ScrubkitSemanticKernelExtensionsTests : IDisposable
    {
        private readonly string _dir;

        public ScrubkitSemanticKernelExtensionsTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "scrubkit-sk-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
        }

        private string WriteFile(string name, string content)
        {
            var path = Path.Combine(_dir, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public async Task SaveChunksAsync_IEnumerable_SavesCorrectly()
        {
            // Arrange
            var memory = new MockSemanticTextMemory();
            var chunks = new List<Chunk>
            {
                new Chunk
                {
                    Path = "E:/Learn/Scrubkit/file1.txt",
                    Name = "file1.txt",
                    TypeBucket = "Text",
                    Index = 0,
                    Count = 2,
                    StartOffset = 0,
                    Text = "Hello World!",
                    Metadata = new Dictionary<string, string> { { "Author", "John Doe" } }
                },
                new Chunk
                {
                    Path = "E:/Learn/Scrubkit/file1.txt",
                    Name = "file1.txt",
                    TypeBucket = "Text",
                    Index = 1,
                    Count = 2,
                    StartOffset = 12,
                    Text = "Another chunk.",
                    Metadata = new Dictionary<string, string> { { "Author", "John Doe" } }
                }
            };

            // Act
            var ids = await memory.SaveChunksAsync("test-collection", chunks);

            // Assert
            Assert.Equal(2, ids.Count);
            Assert.Equal("E:/Learn/Scrubkit/file1.txt#chunk0", ids[0]);
            Assert.Equal("E:/Learn/Scrubkit/file1.txt#chunk1", ids[1]);

            Assert.Equal(2, memory.Entries.Count);
            Assert.Equal("Hello World!", memory.Entries[0].Text);
            Assert.Equal("file1.txt", memory.Entries[0].Description);
            Assert.Contains("John Doe", memory.Entries[0].AdditionalMetadata);
        }

        [Fact]
        public async Task SaveChunksAsync_IAsyncEnumerable_SavesCorrectly()
        {
            // Arrange
            var memory = new MockSemanticTextMemory();
            var chunks = new List<Chunk>
            {
                new Chunk
                {
                    Path = "E:/file2.txt",
                    Name = "file2.txt",
                    TypeBucket = "Text",
                    Index = 0,
                    Count = 1,
                    StartOffset = 0,
                    Text = "Async chunk text.",
                    Metadata = new Dictionary<string, string>()
                }
            };

            async IAsyncEnumerable<Chunk> GetChunksAsync()
            {
                await Task.Yield();
                yield return chunks[0];
            }

            // Act
            var ids = await memory.SaveChunksAsync("test-collection", GetChunksAsync());

            // Assert
            Assert.Single(ids);
            Assert.Equal("E:/file2.txt#chunk0", ids[0]);
            Assert.Single(memory.Entries);
            Assert.Equal("Async chunk text.", memory.Entries[0].Text);
        }

        [Fact]
        public async Task ImportFolderAsync_ISemanticTextMemory_ImportsAndRedacts()
        {
            // Arrange
            WriteFile("sample.txt", "My email is test@example.com.");
            var memory = new MockSemanticTextMemory();
            var scrubber = new FolderScrubber(new ReadOptions { Redaction = RedactionLevel.Standard });

            // Act
            var ids = await memory.ImportFolderAsync("test-collection", scrubber, _dir);

            // Assert
            Assert.Single(ids);
            Assert.Single(memory.Entries);
            var entry = memory.Entries[0];
            Assert.Contains("[EMAIL]", entry.Text);
            Assert.DoesNotContain("test@example.com", entry.Text);
        }

        [Fact]
        public async Task UpsertChunksAsync_IEnumerable_UpsertsMappedRecords()
        {
            // Arrange
            var collection = new MockVectorStoreRecordCollection<string, TestVectorRecord>(r => r.Id);
            var chunks = new List<Chunk>
            {
                new Chunk
                {
                    Path = "E:/file3.txt",
                    Name = "file3.txt",
                    TypeBucket = "Text",
                    Index = 0,
                    Count = 1,
                    StartOffset = 0,
                    Text = "Vector store test",
                    Metadata = new Dictionary<string, string>()
                }
            };

            Func<Chunk, TestVectorRecord> mapper = c => new TestVectorRecord
            {
                Id = $"{c.Path}#{c.Index}",
                Text = c.Text,
                SourceFile = c.Name,
                ChunkIndex = c.Index
            };

            // Act
            var keys = await collection.UpsertChunksAsync(chunks, mapper);

            // Assert
            Assert.Single(keys);
            Assert.Equal("E:/file3.txt#0", keys[0]);
            Assert.Single(collection.UpsertedRecords);
            Assert.Equal("Vector store test", collection.UpsertedRecords[0].Text);
            Assert.Equal("file3.txt", collection.UpsertedRecords[0].SourceFile);
        }

        [Fact]
        public async Task UpsertChunksAsync_IAsyncEnumerable_UpsertsMappedRecords()
        {
            // Arrange
            var collection = new MockVectorStoreRecordCollection<string, TestVectorRecord>(r => r.Id);
            var chunks = new List<Chunk>
            {
                new Chunk
                {
                    Path = "E:/file4.txt",
                    Name = "file4.txt",
                    Index = 0,
                    Text = "Async vector text",
                }
            };

            async IAsyncEnumerable<Chunk> GetChunksAsync()
            {
                await Task.Yield();
                yield return chunks[0];
            }

            Func<Chunk, TestVectorRecord> mapper = c => new TestVectorRecord
            {
                Id = $"{c.Path}#{c.Index}",
                Text = c.Text,
                SourceFile = c.Name,
                ChunkIndex = c.Index
            };

            // Act
            var keys = await collection.UpsertChunksAsync(GetChunksAsync(), mapper);

            // Assert
            Assert.Single(keys);
            Assert.Equal("E:/file4.txt#0", keys[0]);
            Assert.Single(collection.UpsertedRecords);
        }

        [Fact]
        public async Task ImportFolderAsync_IVectorStore_ImportsAndUpserts()
        {
            // Arrange
            WriteFile("sample.txt", "Some generic plain text here.");
            var collection = new MockVectorStoreRecordCollection<string, TestVectorRecord>(r => r.Id);
            var scrubber = new FolderScrubber();

            Func<Chunk, TestVectorRecord> mapper = c => new TestVectorRecord
            {
                Id = $"{c.Path}#{c.Index}",
                Text = c.Text,
                SourceFile = c.Name,
                ChunkIndex = c.Index
            };

            // Act
            var keys = await collection.ImportFolderAsync(scrubber, _dir, mapper);

            // Assert
            Assert.Single(keys);
            Assert.Single(collection.UpsertedRecords);
            Assert.Contains("Some generic plain text", collection.UpsertedRecords[0].Text);
        }
    }

    public class MockSemanticTextMemory : ISemanticTextMemory
    {
        public List<MemoryEntry> Entries { get; } = new List<MemoryEntry>();

        public Task<string> SaveInformationAsync(
            string collection,
            string text,
            string id,
            string? description = null,
            string? additionalMetadata = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            Entries.Add(new MemoryEntry
            {
                Collection = collection,
                Text = text,
                Id = id,
                Description = description,
                AdditionalMetadata = additionalMetadata
            });
            return Task.FromResult(id);
        }

        public Task<string> SaveReferenceAsync(
            string collection,
            string text,
            string externalId,
            string externalSourceName,
            string? description = null,
            string? additionalMetadata = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<MemoryQueryResult?> GetAsync(
            string collection,
            string key,
            bool withEmbedding = false,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task RemoveAsync(
            string collection,
            string key,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<MemoryQueryResult> SearchAsync(
            string collection,
            string query,
            int limit = 1,
            double minRelevanceScore = 0,
            bool withEmbeddings = false,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IList<string>> GetCollectionsAsync(Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    public class MemoryEntry
    {
        public string Collection { get; set; } = "";
        public string Text { get; set; } = "";
        public string Id { get; set; } = "";
        public string? Description { get; set; }
        public string? AdditionalMetadata { get; set; }
    }

    public class TestVectorRecord
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public string SourceFile { get; set; } = "";
        public int ChunkIndex { get; set; }
    }

    public class MockVectorStoreRecordCollection<TKey, TRecord> : IVectorStoreRecordCollection<TKey, TRecord>
        where TKey : notnull
    {
        public List<TRecord> UpsertedRecords { get; } = new List<TRecord>();
        public Func<TRecord, TKey> KeySelector { get; }

        public MockVectorStoreRecordCollection(Func<TRecord, TKey> keySelector)
        {
            KeySelector = keySelector;
        }

        public string CollectionName => "mock-collection";

        public Task<TKey> UpsertAsync(TRecord record, UpsertRecordOptions? options = null, CancellationToken cancellationToken = default)
        {
            UpsertedRecords.Add(record);
            return Task.FromResult(KeySelector(record));
        }

        public async IAsyncEnumerable<TKey> UpsertBatchAsync(IEnumerable<TRecord> records, UpsertRecordOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            foreach (var r in records)
            {
                UpsertedRecords.Add(r);
                yield return KeySelector(r);
            }
        }

        public Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateCollectionAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateCollectionIfNotExistsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteCollectionAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(TKey key, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(IEnumerable<TKey> keys, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TRecord?> GetAsync(TKey key, GetRecordOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TRecord>> GetAsync(IEnumerable<TKey> keys, GetRecordOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        
        public IAsyncEnumerable<TRecord> GetBatchAsync(IEnumerable<TKey> keys, GetRecordOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteBatchAsync(IEnumerable<TKey> keys, DeleteRecordOptions? options = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<VectorSearchResults<TRecord>> VectorizedSearchAsync<TVector>(TVector vectorizedQuery, VectorSearchOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}

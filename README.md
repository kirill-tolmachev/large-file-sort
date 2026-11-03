# Large file sort

Sorts a text file of lines like `415. Apple` by the string part, then by the number. Files can be up to ~100 GB, so this is an external merge sort. Two tools: a generator for test files and the sorter.

## TL;DR

This program uses a parallel merge sort and several optimization techniques to achieve 56 s and 1.46 GB peak memory for a 10 GB file on my local machine. The main ones: sorting raw bytes without decoding strings, an 8-byte key prefix packed into a `ulong` so most comparisons are a single integer compare, sorting a compact line index instead of moving the lines, sampled splitters that let the runs be merged partition by partition on all cores, and a strictly sequential output write.

I used Claude to help me brainstorm and architect the algorithm, to pick the techniques and to write routine code. I tried not to overcomplicate the design. More performance can be squeezed out, but I prefer not to over-optimize unless needed.

## Usage

Needs the .NET 10 SDK. Warnings are errors and the analyzers run at `latest-recommended`.

```bash
dotnet build -c Release
```

```bash
dotnet run -c Release --project src/Generator -- 10G input.txt --seed 42
```

```bash
dotnet run -c Release --project src/Sorter -- input.txt sorted.txt --memory 4G
```

```bash
dotnet test
```

Sizes take a `K`, `M`, `G` or `T` suffix (powers of 1024). Without `--seed` the generator picks a random one and doesn't tell you, so pass one if you'll need the same file again. `--words <file>` replaces the built-in list of 300 English words with your own, one ASCII word per line.

For the sorter, `--memory` (default `2G`, minimum `1M`) is a rough budget rather than a hard limit, and `--temp <dir>` is where the run files go, by default next to the output.

Both tools print nothing on success. Exit code 1 means bad arguments, 2 means an I/O or data error (missing file, malformed line, unwritable output), with the reason on stderr. Both run on Linux too.

## The generator

Writes 16 MB blocks in parallel, each from its own seed, so the output is the same for the same seed and size. A line gets a number in -999999..999999 and a phrase of 1-6 words. Half the phrases come from a fixed pool of 10,000, so plenty of lines share a string and the number actually matters for the order. The file is cut at a line boundary, so it's at most the requested size.

## The sorter

Everything works on raw bytes; strings are never decoded.

1. **Sample.** Read ~256 small blocks spread over the input, sort the lines found there and take quantiles as splitter keys. They divide the key space into partitions of a few MB each (225 for a 1 GB input with the defaults on 28 cores).

2. **Sort runs.** Four workers pull chunks from the input (96 MB each with the default budget). Every line becomes a 32-byte struct holding the parsed number, the line's offsets and the first 8 text bytes packed into a `ulong`. Comparing those integers gives the same order as comparing the bytes, so most comparisons never touch the text. Lines are bucketed by partition, the buckets are sorted in parallel on all cores, and the chunk goes out as one run file with the bucket offsets recorded.

3. **Merge.** Partitions don't overlap, so partition `p` of the output is just the merge of section `p` from every run. Each core grabs the next partition, merges it into a buffer with a priority queue, then waits for its turn to append the buffer to the output. The merging is parallel but the output is still written front to back. (I first wrote partitions in parallel at their final offsets. Don't: NTFS zero-fills the gap in front of an out-of-order write, about 200 s for an 8 GB gap, and the sparse file got slower as it filled.)

Three quarters of the memory budget go to the sort workers, the rest to merge buffers. The run count is input size / chunk size, so 100 GB on the default 2 GB budget means ~1,070 runs and the per-run read buffers shrink to their 16 KB minimum. `--memory 8G` gets that down to ~270 runs and is noticeably faster at that size.

The output is assembled in a temp file next to the final path and renamed at the end. A failed sort leaves nothing behind, an existing output is only replaced on success, and you can sort a file onto itself. If a worker fails, the others are cancelled and its error is reported, with the byte offset for a malformed line.

## Assumptions

- Text is ASCII. UTF-8 works too and sorts by code point, since comparison is byte-wise.
- The number fits in `Int64`, may be negative, and is followed by exactly `. ` (dot, space).
- Lines end with LF. CRLF is accepted and kept; a missing final newline is added.
- A single line must fit in one chunk.
- The order of lines that are completely identical is unspecified.
- Free space: the input size again for the run files, plus the output.

## Numbers

Windows 11, 28 logical cores, 64 GB RAM, Samsung 990 EVO Plus, default 2 GB budget. Wall clock for the whole process, peak working set as reported by the OS.

| Input | Lines | Time | Peak working set |
|---|---|---|---|
| 1 GB | 39.9 M | 5.0 s | 1.43 GB |
| 10 GB | 401 M | 56 s | 1.46 GB |

For 1 GB about 4 s is sorting runs and 1 s merging. The 10 GB case is disk-bound: the merge reads the runs back while the file system is still flushing them, while also writing the output. Both outputs were checked with a separate tool for sorted order and for being a permutation of the input (same line count, same order-independent hash of all lines).

## Layout

```text
src/Common      line parsing and comparison, size parsing
src/Generator   test file generator and the built-in word list
src/Sorter      the external merge sort
tests/Tests     xUnit tests
```

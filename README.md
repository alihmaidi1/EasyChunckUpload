# Chunk Upload Library

[![NuGet Version](https://img.shields.io/nuget/v/ChunkUpload.svg)](https://www.nuget.org/packages/ChunkUpload/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Build Status](https://img.shields.io/github/actions/workflow/status/yourusername/chunk-upload/build.yml)](https://github.com/yourusername/chunk-upload/actions)
[![Code Coverage](https://img.shields.io/codecov/c/github/yourusername/chunk-upload)](https://codecov.io/gh/yourusername/chunk-upload)

A robust .NET library for handling large file uploads using chunking strategy with resume capabilities and automatic cleanup.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Storage Providers](#storage-providers)
- [API Reference](#api-reference)
- [Testing](#testing)
- [Contributing](#contributing)
- [License](#license)

## Features <a name="features"></a>

- Chunked file uploads with configurable chunk size
- Resume interrupted uploads
- Automatic cleanup of orphaned chunks
- Multiple storage provider support
- Progress tracking
- File validation and integrity checks
- Cross-platform compatibility

## Installation <a name="installation"></a>

```bash
dotnet add package ChunkUpload

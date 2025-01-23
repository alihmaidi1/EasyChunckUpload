# ChunkUpload Service

[![NuGet Version](https://img.shields.io/nuget/v/ChunkUploadService.svg?style=flat-square)](https://www.nuget.org/packages/ChunkUploadService/)
[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)
[![.NET Core](https://img.shields.io/badge/.NET-9.0%2B-blue.svg?style=flat-square)](https://dotnet.microsoft.com/)

A robust implementation for handling large file uploads using chunking strategy with resumable capabilities and concurrent processing support.

## Table of Contents
- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [API Documentation](#api-documentation)
- [Error Handling](#error-handling)
- [Performance](#performance)
- [Contributing](#contributing)
- [License](#license)

## Features <a name="features"></a>
- 🚀 **Resumable Uploads** - Continue interrupted uploads
- ⚡ **Chunk Validation** - Automatic chunk order verification
- 🔒 **Concurrency Control** - Thread-safe operations with SemaphoreSlim
- 📁 **Atomic Merging** - Temp file strategy for data integrity
- 📊 **Progress Tracking** - Real-time upload status monitoring
- 🧹 **Auto-Cleanup** - Configurable temp file retention

## Installation <a name="installation"></a>
```bash
# Package Manager
Install-Package ChunkUploadService

# .NET CLI
dotnet add package ChunkUploadService

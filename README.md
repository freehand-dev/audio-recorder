# audio-recording
> A simple Linux application for audio recording via ALSA, with support for AOT compilation.

The program records to **WAV** files with support for:
- **Automatic file splitting** by duration (e.g., every 10 minutes).
- **Automatic cleanup** of old files (by number of files or retention time).

---

## Features

- AOT compiled for Linux
- ALSA audio capture (`hw:0,0` or another device)
- Recording saved as `.wav` with correct headers
- Configurable via **command-line arguments** or **YAML configuration file**
- Automatic cleanup: Keep only the latest N files or delete old files by age

---

## YAML Configuration

The program can be configured via a `audio-recorder.yaml` file.

### Example:

```yaml
Logging:
  LogLevel:
    Default: Information
    Microsoft.Hosting.Lifetime: Information

Recorder:
  Enabled: true
  Directory: recordings
  FileNameFormat: 'audio_{date:yyyyMMdd}_{time:HHmmss}_{num:00000}.wav'
  MaxFileDurationMinutes: 15
  DeviceName: plughw:3,0
  SampleRate: 44100

Cleaner:
  Enabled: true
  MaxFilesToKeep: 100
```

### Parameters

#### Recorder
- `Enabled` – Enable/disable recording.  
- `Directory` – Directory for saving recordings.  
- `FileNameFormat` – Filename template (`{date}`, `{time}`, `{num}`).  
- `MaxFileDurationMinutes` – Maximum length of a single file.  
- `DeviceName` – ALSA device name (`plughw:X,Y`).  
- `SampleRate` – Audio sampling rate.  

#### Cleaner
- `Enabled` – Enable/disable automatic cleanup.  
- `MaxFilesToKeep` – Number of most recent files to keep. Older ones will be deleted.  


## Building (AOT)

To publish a self-contained **native binary**:

```bash
dotnet publish -r linux-arm64 -c Release /p:PublishAot=true
```

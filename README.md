# YoutubeAutomation - YouTube Documentary Automation

## วัตถุประสงค์ของโปรเจค

**YoutubeAutomation** คือแอปพลิเคชัน Windows Desktop ที่สร้างวิดีโอ YouTube สารคดีอัตโนมัติ สำหรับช่อง **"เรื่องแปลก น่ารู้"** - ช่อง YouTube สไตล์พอดแคสต์สารคดีเรื่องแปลก

โปรแกรมมี 2 โหมด:
- **Main Flow** - วิดีโอภาพปกเดียว (ภาพนิ่ง + เสียง)
- **Multi-Image Flow** - วิดีโอหลายภาพ เปลี่ยนตาม scene พร้อม Ken Burns Effect + Crossfade + BGM
  - เลือกใช้ **SD Forge Local** (ฟรี) หรือ **Google Gemini Cloud** (ไม่ต้องติดตั้ง SD)

---

## ที่มา: จาก Manual สู่ Automation

### ปัญหา: Manual Workflow ที่ใช้เวลามาก

ก่อนมีโปรแกรมนี้ ผู้สร้างต้องทำ 3 ขั้นตอนด้วยตัวเองทุก Episode:

#### ขั้นตอนที่ 1: คิดหัวข้อ (Topic Brainstorming)
```
ภารกิจ: เสนอหัวข้อ "เรื่องแปลกที่ท้าทายความจริง" มา 5 หัวข้อ
- ทุกหัวข้อต้องขึ้นต้นด้วย "ทำไม?" (Why?)
- เป็นเรื่องลึกลับ มีรายละเอียดเชิงวิทยาศาสตร์หรือประวัติศาสตร์
- เนื้อหาต้องขยายเป็นบทพูด 15 นาที (~2,200 คำ) ได้
```

#### ขั้นตอนที่ 2: สร้างบทพูด (Script Generation)
```
เขียนบทพูดภาษาไทยแบ่งเป็น 3 ส่วน (ส่วนละ ~5 นาที)
Format: Read aloud in an informative, lower pitch, and mysterious tone
มี [Pause] และใช้ภาษาสละสลวยเหมือนสารคดีระดับโลก
```

#### ขั้นตอนที่ 3: สร้าง Prompt รูปปก
```
Style: Vintage scientific illustration, 19th-century etching style
Technique: Bold black outlines, halftone dot shading, aged paper texture
Color Palette: Muted colors (สีเหลืองทราย, เขียวตุ่น, ฟ้าหม่น)
```

### Solution: โปรแกรม Automate ทั้งหมด + เพิ่มเติม

โปรแกรมนี้ทำ **ทุกขั้นตอนอัตโนมัติ** และเพิ่มความสามารถ:

| ขั้นตอน | Manual | โปรแกรมนี้ |
|---------|--------|-----------|
| 1. คิดหัวข้อ | Copy prompt ไป AI | กดปุ่มเดียว |
| 2. เขียนบท | Copy prompt 3 รอบ | กดปุ่มเดียว |
| 3. สร้างรูปปก | Copy prompt + generate เอง | กดปุ่มเดียว |
| 4. สร้างเสียง | เอาบทไป Google TTS เอง | **อัตโนมัติ** |
| 5. รวมเป็นวิดีโอ | เปิด editor ตัดต่อเอง | **อัตโนมัติ** (FFmpeg) |
| 6. **Multi-Image** | ต้องทำภาพหลายภาพเอง | **อัตโนมัติ** (SD Local / Cloud + FFmpeg 2-pass) |
| 7. **BGM** | หา BGM + ปรับเสียงเอง | **อัตโนมัติ** (16 built-in tracks + AI mood analysis) |

**ผลลัพธ์:** จาก ~2-3 ชั่วโมงต่อ Episode เหลือกด "Run All Flow" รอ ~10-15 นาที ได้วิดีโอพร้อม upload

---

## Tech Stack & Architecture

### Language & Framework
- **Language:** C# (.NET 8.0)
- **UI Framework:** WPF (Windows Presentation Foundation)
- **UI Theme:** Material Design (MaterialDesignThemes 5.3.0)
- **Pattern:** MVVM (CommunityToolkit.Mvvm) + Dependency Injection
- **Target:** Windows Desktop Application

### Project Structure
```
YoutubeAutomation/
├── YoutubeAutomation.sln              # Solution file
└── YoutubeAutomation/                 # Main project
    ├── App.xaml.cs                   # Entry point + DI + Global Exception Handlers
    ├── MainWindow.xaml               # Main UI (7-step wizard)
    ├── MultiImageWindow.xaml         # Multi-Image UI (6-step wizard)
    ├── Converters.cs                 # WPF value converters
    ├── Models/
    │   ├── VideoProject.cs           # Project data container
    │   ├── ProjectSettings.cs        # Configuration (API keys, paths, SD settings)
    │   ├── ProjectState.cs           # Workflow state persistence
    │   ├── MultiImageState.cs        # Multi-Image state persistence
    │   ├── ContentCategory.cs        # Per-category config (prompts, TTS tone, image style, BGM)
    │   ├── ContentCategoryRegistry.cs # Static registry (4 active + 4 future categories)
    │   ├── SceneData.cs              # Scene + ScenePart models
    │   ├── WorkflowStep.cs           # Step status tracking
    │   ├── BgmLibrary.cs             # Built-in BGM library (16 tracks, 8 moods × 2)
    │   ├── BgmOptions.cs             # BGM configuration (volume, fade in/out)
    │   └── BgmTrackDisplayItem.cs    # UI display wrapper for BGM dropdown
    ├── Services/
    │   ├── Interfaces/
    │   │   ├── IOpenRouterService.cs
    │   │   ├── IGoogleTtsService.cs
    │   │   ├── IFfmpegService.cs
    │   │   ├── IDocumentService.cs
    │   │   └── IStableDiffusionService.cs
    │   ├── OpenRouterService.cs      # AI text/image generation (OpenRouter)
    │   ├── GoogleTtsService.cs       # Text-to-Speech (Google Gemini TTS)
    │   ├── FfmpegService.cs          # Video encoding (single + multi-image)
    │   ├── DocumentService.cs        # DOCX export
    │   ├── StableDiffusionService.cs # Local SD Forge API (auto-detect port)
    │   └── AppLogger.cs              # File-based crash logging
    ├── ViewModels/
    │   ├── MainViewModel.cs          # Main flow logic
    │   └── MultiImageViewModel.cs    # Multi-image flow logic
    └── Prompts/
        └── PromptTemplates.cs        # AI prompt templates (category-aware)
```

### Dependencies (NuGet Packages)
| Package | Version | Purpose |
|---------|---------|---------|
| CommunityToolkit.Mvvm | 8.4.0 | MVVM pattern (source-gen attributes) |
| MaterialDesignThemes | 5.3.0 | Modern Material Design UI |
| Microsoft.Extensions.DependencyInjection | 10.0.2 | DI container |
| DocumentFormat.OpenXml | 3.4.1 | Export scripts as DOCX |
| NAudio | 2.2.1 | Audio playback + WAV handling |
| Newtonsoft.Json | 13.0.4 | JSON serialization |

### External Tools Required
- **FFmpeg** - Video encoding (ต้องมี ffprobe ด้วย สำหรับ Multi-Image mode)
- **Stable Diffusion Forge** (Optional) - สำหรับ Multi-Image mode เท่านั้น

---

## สองโหมดการทำงาน

### Mode 1: Main Flow (ภาพปกเดียว) — 7 Steps

```
Input: หัวข้อ
   │
   ▼
[Step 1] สร้างหัวข้อ ──► 5 หัวข้อ (OpenRouter AI)
   │
   ▼
[Step 2] สร้างบทพูด ──► 3 Parts (เกริ่น / เนื้อหา / สรุป)
   │
   ├──► [Step 3] สร้างรูปปก ──► 1 ภาพ (OpenRouter AI)
   │
   ├──► [Step 4] สร้างเสียง ──► 3 WAV files (Google Gemini TTS)
   │
   ▼
[Step 5] สร้างวิดีโอ ──► ภาพนิ่ง + เสียง → MP4 (FFmpeg)
   │
   ▼
[Step 6] บันทึก ──► DOCX, TXT, WAV, PNG, MP4
   │
   ▼
[Step 7] YouTube Info ──► Title, Description, Tags
```

### Mode 2: Multi-Image Flow (หลายภาพ + transitions + BGM) — 6 Steps

```
Input: หัวข้อ + ภาพปก
   │
   ▼
[Step 1] สร้างบท Scene ──► 3 Parts × 5-8 scenes = 15-24 scenes (JSON)
                           แต่ละ scene มี: text (ไทย) + image_prompt (EN)
   │
   ▼
[Step 2] ตรวจ/แก้ไข ──► แก้บทพูด + image prompt ต่อ scene ได้
   │
   ▼                      ┌─────────────────────────────────────────────┐
[Step 3] สร้างภาพ+เสียง ──►│ ภาพ (SD Local / Cloud)  ←─parallel─►  เสียง (Google TTS) │
                           └─────────────────────────────────────────────┘
                           Image Generation:
                           - SD Local: SD Forge (ฟรี, ต้องมี GPU)
                           - Cloud: Google Gemini (ไม่ต้อง GPU, มีค่าใช้จ่าย)
                           - Scene 1 ใช้ภาพปกอัตโนมัติ (ไม่ gen ใหม่)
                           - รองรับ Scene Chaining (img2img ต่อเนื่อง)
   │
   ▼
[Step 4] สร้างวิดีโอ ──► FFmpeg 2-Pass:
                          Pass 1: zoompan clip ต่อภาพ (Ken Burns)
                          Pass 2: xfade รวมทั้งหมด + audio + BGM → MP4
                          BGM: auto-loop + fade in/out + audio ducking
   │
   ▼
[Step 5] เสร็จ ──► เปิดโฟลเดอร์ / เปิดวิดีโอ
```

---

## API Services

### 1. OpenRouter API
**URL:** `https://openrouter.ai/api/v1/chat/completions`

| Function | Default Model | Purpose |
|----------|-------|---------|
| Topic Generation | `google/gemini-2.5-flash` | สร้าง 5 หัวข้อ |
| Script Generation | `google/gemini-2.5-flash` | เขียนบท 3 ส่วน |
| Scene-Based Script | `google/gemini-2.5-flash` | เขียนบทแบ่ง scene (JSON) |
| Image Prompt | `google/gemini-2.5-flash` | สร้าง English prompt |
| Image Generation | `google/gemini-2.0-flash-exp:free` | สร้างรูปปก (Main flow) |

**Category-aware:** ทุก function รับ `category` parameter เพื่อปรับ prompt ตามหมวดเนื้อหา (animal, body, history, space)

**แนะนำ:** สำหรับภาพปก ใช้ **Google Imagen** ผ่าน Gemini API จะได้ภาพที่สวยที่สุด (ราคา $0.02-0.06/ภาพ)

**Authentication:** Bearer Token (OpenRouter API Key)
**Get API Key:** https://openrouter.ai/keys

### 2. Google Generative Language API (TTS)
**URL:** `https://generativelanguage.googleapis.com/v1beta/models`

| Setting | Value |
|---------|-------|
| Model (Default) | `gemini-2.5-pro-preview-tts` (คุณภาพสูงสุด) |
| Model (ฟรี) | `gemini-2.5-flash-preview-tts` (มี Free Tier, คุณภาพต่ำกว่า) |
| Sample Rate | 24000 Hz |
| Output | WAV (16-bit PCM) |

**Available Voices:** charon, kore, fenrir, aoede, puck, zephyr, orus, leda
**Category-aware:** รับ `ttsInstruction` parameter เพื่อปรับน้ำเสียงตามหมวดเนื้อหา

**Get API Key:** Google Cloud Console → Enable "Generative Language API"

### 3. Stable Diffusion Forge (Local)
**URL:** Auto-detect (scans ports 7860-7863)

| Setting | Default |
|---------|---------|
| Endpoint | `POST /sdapi/v1/txt2img` / `img2img` |
| Health Check | `GET /sdapi/v1/options` |
| Resolution (SD 1.5) | 768x432 |
| Resolution (SDXL) | 1152x648 |
| Steps | 25 |
| CFG Scale | Auto (SD 1.5: 8.5, SDXL: 6.0) |
| Sampler | DPM++ 2M Karras |

Features:
- **Auto-detect port** — scan 7860-7863 อัตโนมัติ ไม่ต้องตั้ง URL ด้วยมือ
- **Model selection** — เลือก model ใน dropdown, auto-detect SDXL models
- **SDXL-aware** — ปรับ resolution + CFG ตาม model อัตโนมัติ
- **Scene Chaining** — img2img ต่อเนื่องจาก scene ก่อนหน้า เพื่อความสม่ำเสมอ
- **Reference image resize** — ย่อขนาดก่อนส่ง img2img เพื่อลด VRAM
- **Retry + Fallback** — retry 5xx errors + fallback จาก img2img ไป txt2img
- **Consecutive failure abort** — หยุดอัตโนมัติเมื่อ error ติดต่อกัน 3 ครั้ง

### 4. Cloud Image Generation (Google Gemini)
**ทางเลือกใหม่** สำหรับ Multi-Image mode — ไม่ต้องติดตั้ง Stable Diffusion

| Setting | Value |
|---------|-------|
| Toggle | `UseCloudImageGen` ใน Settings |
| Default Model | `gemini-2.5-flash-image` |
| API | Google Generative Language API |
| Aspect Ratio | 16:9 (landscape) |

**ข้อดี:** ไม่ต้องมี GPU, ไม่ต้องติดตั้ง SD Forge, ภาพคุณภาพสูง
**ข้อเสีย:** มีค่าใช้จ่ายต่อภาพ (~$0.01-0.03/ภาพ), ต้องใช้ internet

### 5. FFmpeg (Local Tool)

**Multi-Image Flow (2-Pass):**
1. **Pass 1:** สร้าง zoompan clip ต่อภาพ (Ken Burns effect, สลับ zoom-in/zoom-out)
2. **Pass 2:** รวม clips ด้วย xfade (fade transition 1 วินาที) + audio merge

**Encoding Options:**
- **CPU:** `libx264 -preset fast`
- **GPU:** `h264_nvenc -preset p1 -tune hq` (NVIDIA NVENC)

---

## Background Music (BGM)

Multi-Image mode รองรับ **เพลงประกอบอัตโนมัติ** พร้อม audio ducking

### Built-in Library (16 tracks — 8 moods × 2 variants)

**Original 8 tracks:**
| Track | Mood | ใช้กับเนื้อหาแบบ |
|-------|------|------------------|
| `curious-discover.mp3` | Curious | สำรวจ ค้นพบ |
| `curious-wonder.mp3` | Curious | น่าสงสัย ลึกลับ |
| `upbeat-fun.mp3` | Upbeat | สนุก ตลก |
| `upbeat-lively.mp3` | Upbeat | มีชีวิตชีวา |
| `gentle-nature.mp3` | Gentle | ธรรมชาติ สงบ |
| `gentle-soothing.mp3` | Gentle | ผ่อนคลาย |
| `emotional-heartfelt.mp3` | Emotional | ซาบซึ้ง |
| `emotional-warm.mp3` | Emotional | อบอุ่น |

**เพิ่มใหม่ 8 tracks (Session 6):**
| Track | Mood | ใช้กับเนื้อหาแบบ |
|-------|------|------------------|
| `mysterious-suspense.mp3` | Mysterious | ตื่นเต้น ลึกลับ |
| `mysterious-wonder.mp3` | Mysterious | น่าพิศวง |
| `dramatic-tension.mp3` | Dramatic | ตึงเครียด |
| `dramatic-cinematic.mp3` | Dramatic | ภาพยนตร์ สมจริง |
| `epic-grandeur.mp3` | Epic | ยิ่งใหญ่ อลังการ |
| `epic-cosmic.mp3` | Epic | จักรวาล อวกาศ |
| `playful-bounce.mp3` | Playful | สนุกสนาน |
| `playful-quirky.mp3` | Playful | แปลกๆ น่ารัก |

**ที่เก็บ:** `%APPDATA%\YoutubeAutomation\bgm\`

### Features
- **AI Mood Analysis** — วิเคราะห์บทพูดแล้วแนะนำ BGM ที่เหมาะสมอัตโนมัติ
- **Custom BGM** — browse เลือกไฟล์เพลงของตัวเองได้
- **Volume Control** — default 0.25 (ปรับได้ 0.0-1.0)
- **Fade In/Out** — เข้า 3 วินาที / ออก 5 วินาที
- **Audio Ducking** — ลดเสียง BGM อัตโนมัติขณะมีเสียงบรรยาย
- **Auto-loop** — BGM วนซ้ำจนจบวิดีโอ
- **Preview** — ฟังตัวอย่าง BGM ก่อนใช้ได้

---

## Content Categories (หมวดเนื้อหา)

โปรแกรมรองรับ **ระบบหมวดเนื้อหา** ที่ปรับ prompt, เสียง, สไตล์ภาพ, BGM และ hashtags ให้เหมาะกับแต่ละประเภทเนื้อหาอัตโนมัติ

### 4 หมวดที่ใช้งานได้
| Key | ชื่อ | เนื้อหา | BGM Default |
|-----|------|---------|-------------|
| `animal` | สัตว์โลกแปลก | สัตว์ลึกลับ พฤติกรรมแปลก | Curious |
| `body` | ร่างกาย | ความลับของร่างกายมนุษย์ | Curious |
| `history` | ประวัติศาสตร์ | เหตุการณ์ลึกลับในอดีต | Mysterious |
| `space` | อวกาศ | ดาราศาสตร์ จักรวาลวิทยา | Epic |

### 4 หมวดสำรอง (รอเปิดใช้)
`food` (อาหาร), `tech` (เทคโนโลยี), `psychology` (จิตวิทยา), `nature` (ธรรมชาติ)

### สิ่งที่แต่ละหมวดกำหนด
- **Topic Generation** — กฎการตั้งหัวข้อ, ตัวอย่างดี/ไม่ดี
- **Script Tone** — น้ำเสียงบทพูด, โครงสร้างเนื้อหา
- **TTS Voice Instruction** — คำแนะนำน้ำเสียงสำหรับ Google TTS
- **Image Style** — สไตล์ภาพ, สี, เทคนิค (แยก SD Local / Cloud)
- **BGM Mood** — อารมณ์เพลงประกอบเริ่มต้น + คำอธิบายแต่ละ mood
- **YouTube Info** — hashtags, คำที่ต้องตัดออกจากหัวข้อ

### Architecture
- `ContentCategory` — model เก็บ config ทั้งหมดของแต่ละหมวด
- `ContentCategoryRegistry` — static registry, `GetByKey(null)` → fallback เป็น Animal (backward compatible)
- หมวดจะถูกเก็บใน `MultiImageState.CategoryKey` และ `ProjectSettings.DefaultCategoryKey`

---

## Quick Start

### Prerequisites
1. **Windows 10/11** with .NET 8.0 Runtime
2. **FFmpeg** installed ([download](https://ffmpeg.org/download.html)) — ต้องมี `ffprobe` ด้วย
3. **API Keys:**
   - OpenRouter API Key → https://openrouter.ai/keys
   - Google API Key → Google Cloud Console → Enable "Generative Language API"
4. **(Optional) Stable Diffusion Forge** สำหรับ Multi-Image mode (ภาพฟรี)
   - Download: https://github.com/lllyasviel/stable-diffusion-webui-forge
   - เปิดด้วย `--api` flag
   - **หรือ** ใช้ Cloud Image Gen (Google Gemini) แทนได้ ไม่ต้องติดตั้ง SD

### Build & Run
```bash
git clone https://github.com/your-username/YoutubeAutomation.git
cd YoutubeAutomation
dotnet build
dotnet run --project YoutubeAutomation
```

### Publish (Self-Contained)
```bash
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
```

### First Time Setup
1. เปิดโปรแกรม → Settings panel (ไอคอนเฟือง)
2. กรอก **OpenRouter API Key**
3. กรอก **Google API Key**
4. ตั้ง **FFmpeg path** (เช่น `C:\ffmpeg\ffmpeg.exe`)
5. ตั้ง **Output folder** path
6. (Optional) ทดสอบ GPU Encoding (NVENC)

Settings จะถูกบันทึกที่:
```
%APPDATA%\YoutubeAutomation\settings.json
```

### Multi-Image Setup (เพิ่มเติม)

**Option A: ใช้ SD Forge Local (ฟรี, ต้องมี GPU)**
1. ติดตั้ง Stable Diffusion Forge
2. เปิด Forge ด้วย `--api` flag
3. กดปุ่ม **"Multi-Image"** ที่ header ของ MainWindow
4. โปรแกรม auto-detect port ของ SD Forge อัตโนมัติ (7860-7863)

**Option B: ใช้ Cloud Image Gen (ไม่ต้อง GPU)**
1. กดปุ่ม **"Multi-Image"** ที่ header ของ MainWindow
2. เปิด toggle **"Use Cloud Image Gen"**
3. เลือก Cloud model (default: `gemini-2.5-flash-image`)
4. ใช้ Google API Key เดียวกับ TTS

**แนะนำสำหรับ GPU 12GB (RTX 3060):**
- ใช้ `--medvram` flag เพื่อลด VRAM usage
- หรือเลือก SD 1.5 model แทน SDXL เพื่อความเสถียร

---

## Configuration

### Settings File
```
%APPDATA%\YoutubeAutomation\settings.json
```

### Required Settings
| Setting | Description |
|---------|-------------|
| `OpenRouterApiKey` | API key from openrouter.ai |
| `GoogleApiKey` | Google API key (for Gemini TTS) |
| `FfmpegPath` | Full path to ffmpeg.exe |
| `OutputBasePath` | Folder to save generated content |

### Optional Settings
| Setting | Default | Description |
|---------|---------|-------------|
| `TopicGenerationModel` | `google/gemini-2.5-flash` | Model for topic generation |
| `ScriptGenerationModel` | `google/gemini-2.5-flash` | Model for script writing |
| `TtsModel` | `gemini-2.5-pro-preview-tts` | TTS model (คุณภาพสูงสุด) |
| `TtsVoice` | `charon` | TTS voice |
| `UseGpuEncoding` | `false` | Use NVIDIA NVENC for video encoding |
| `StableDiffusionUrl` | `http://127.0.0.1:7860` | SD Forge URL (auto-detected) |
| `StableDiffusionSteps` | `25` | Image generation steps |
| `StableDiffusionCfgScale` | `8.5` / `6.0` | CFG Scale (auto: SD1.5=8.5, SDXL=6.0) |
| `StableDiffusionModelName` | `""` | Last used SD checkpoint name |
| `StableDiffusionBatPath` | `""` | Path to SD Forge .bat for auto-launch |
| `CloudImageModel` | `gemini-2.5-flash-image` | Cloud image generation model |
| `UseCloudImageGen` | `false` | ใช้ Cloud (Gemini) แทน SD Local |
| `BgmEnabled` | `false` | เปิดใช้เพลงประกอบ |
| `BgmFilePath` | `""` | Path to BGM file |
| `BgmVolume` | `0.25` | BGM volume (0.0-1.0) |
| `DefaultCategoryKey` | `"animal"` | หมวดเนื้อหาเริ่มต้น (animal/body/history/space) |

---

## Output Structure

### Main Flow
```
[OutputBasePath]/
└── EP[N] [Topic Name]/
    ├── หัวข้อเรื่อง.txt
    ├── บท1.txt, บท2.txt, บท3.txt
    ├── บท1.docx, บท2.docx, บท3.docx
    ├── ep[N]_1.wav, ep[N]_2.wav, ep[N]_3.wav
    ├── ปกไทย_ep[N].png
    └── EP[N]_Thai.mp4
```

### Multi-Image Flow
```
[OutputBasePath]/
└── EP[N] [Topic Name]/
    ├── scenes/
    │   ├── scene_000.png   # ภาพปก (copy จาก cover image)
    │   ├── scene_001.png   # Generated by SD Forge / Cloud
    │   ├── ...
    │   └── scene_019.png
    ├── ep[N]_1.wav, ep[N]_2.wav, ep[N]_3.wav
    └── EP[N]_MultiImage.mp4   # 1920x1080 with Ken Burns + crossfade + BGM
```

---

## Logging & Troubleshooting

### Log Files
```
%APPDATA%\YoutubeAutomation\logs\log_YYYYMMDD_HHmmss.txt
```

Logs จะบันทึก:
- ทุก API call (SD txt2img/img2img, response time, payload size)
- Pipeline steps (image gen, audio gen, video creation)
- Errors + stack traces
- Unhandled exceptions (global exception handlers ป้องกัน crash)

### Common Issues

| ปัญหา | สาเหตุ | วิธีแก้ |
|--------|--------|---------|
| เชื่อมต่อ SD ไม่ได้ | Port ไม่ตรง | โปรแกรม auto-detect port อัตโนมัติ |
| SD Forge VRAM error | VRAM ไม่พอ | ใช้ `--medvram` flag หรือเลือก SD 1.5 model |
| ภาพ gen ช้ามาก | XL model + 12GB VRAM | ลด steps, ใช้ SD 1.5 model, หรือเพิ่ม `--medvram` |
| โปรแกรมปิดตัวเอง | ดู log file | เปิด `%APPDATA%\YoutubeAutomation\logs\` |
| img2img ล้มเหลว | VRAM + large reference | โปรแกรม fallback ไป txt2img อัตโนมัติ |

---

## Architecture for Developers

### Key Files

| Priority | File | Description |
|----------|------|-------------|
| 1 | `ViewModels/MainViewModel.cs` | Main flow logic - 7-step wizard |
| 2 | `ViewModels/MultiImageViewModel.cs` | Multi-image flow - parallel pipeline |
| 3 | `Prompts/PromptTemplates.cs` | AI prompts — category-aware (ปรับ output quality ที่นี่) |
| 4 | `Services/OpenRouterService.cs` | OpenRouter API - text/image generation |
| 5 | `Services/GoogleTtsService.cs` | TTS API - PCM to WAV conversion |
| 6 | `Services/FfmpegService.cs` | Video encoding (single + multi-image) |
| 7 | `Services/StableDiffusionService.cs` | SD Forge API (auto-detect, retry, fallback) |
| 8 | `Services/AppLogger.cs` | File-based logging |
| 9 | `Models/ProjectSettings.cs` | Configuration model |
| 10 | `Models/BgmLibrary.cs` | Built-in BGM library (16 tracks, 8 moods × 2) |
| 11 | `Models/ContentCategory.cs` | Per-category config (prompts, TTS, image, BGM, hashtags) |
| 12 | `Models/ContentCategoryRegistry.cs` | Static registry (4 active + 4 future categories) |

### Important Patterns
- `[ObservableProperty]` generates `OnXxxChanged` partial methods via source gen
- Audio timer CTS: use `CancellationTokenSource.CreateLinkedTokenSource()` to link with parent
- `using var cts` in try block causes disposal before catch - manage CTS lifecycle manually
- Lambda closures in for loops: capture `var partIndex = i;` before lambda
- `CommandManager.InvalidateRequerySuggested()` needed after error to re-enable buttons
- Multi-Image pipeline uses `Task.WhenAll` for parallel image + audio generation
- Cross-cancel via `ContinueWith(OnlyOnFaulted)` — if either task fails, cancel the other
- `_isRunAll` flag controls CTS ownership and MessageBox suppression in pipeline mode
- SD image gen uses `SemaphoreSlim(1)` to prevent VRAM overflow
- `_isRestoringState = true` guard when setting SelectedCategory programmatically (prevent re-triggering logic)
- `ObservableCollection.Clear()` triggers ComboBox `SelectedItem=null` → use `_isLoadingModels` guard

### Multi-Image Pipeline Flow
```
RunAllAsync
  │
  ├── GenerateSceneScriptAsync()     [sequential - needs scripts first]
  │
  ├── Task.WhenAll(                  [parallel - independent tasks]
  │     GenerateAllImagesAsync(),
  │     GenerateAllAudioAsync()
  │   )
  │   └── Cross-cancel: if either fails → cancel CTS → stop the other
  │
  ├── Verify completeness            [gate check - all images + audio present?]
  │
  └── CreateVideoAsync()             [sequential - needs all assets]
```

---

## Cost Estimate

| รายการ | Main Flow | Multi-Image (SD Local) | Multi-Image (Cloud) |
|--------|-----------|----------------------|---------------------|
| Script gen (OpenRouter) | ~$0.01 | ~$0.01 | ~$0.01 |
| Cover image (Google Imagen) | ~$0.02-0.04 | - | - |
| Scene images | - | **$0.00** (ฟรี) | ~$0.15-0.45 (15-24 scenes) |
| TTS (Google Gemini Pro) | ~$0.05 | ~$0.05 | ~$0.05 |
| **รวม** | **~$0.08/EP** | **~$0.06/EP** | **~$0.21-0.51/EP** |

### Google Gemini TTS Pricing
โปรเจคนี้ใช้ `gemini-2.5-pro-preview-tts` เป็น default (คุณภาพสูงสุด):

| Model | Free Tier | Paid (Input) | Paid (Audio Output) | คุณภาพ |
|-------|-----------|-------------|-------------------|--------|
| `gemini-2.5-pro-preview-tts` | ไม่มี | $1.00/1M tokens | $20.00/1M tokens | สูงสุด |
| `gemini-2.5-flash-preview-tts` | **ฟรี** | $0.50/1M tokens | $10.00/1M tokens | ดี, เน้นเร็ว |

**Default:** Pro (คุณภาพสูงสุด) — เปลี่ยนเป็น Flash ใน Settings ได้ถ้าต้องการใช้ฟรี

> ดู pricing ล่าสุดที่: https://ai.google.dev/gemini-api/docs/pricing

### Google Imagen สำหรับ Cover Image
แนะนำใช้ **Google Imagen** สำหรับสร้างภาพปก — ให้ภาพที่สวยและสมจริงที่สุด:

| Model | ราคา/ภาพ | คุณภาพ |
|-------|----------|--------|
| Imagen 4 Ultra | $0.06 | สูงสุด |
| Imagen 4 Standard | $0.04 | ดีมาก |
| Imagen 4 Fast | $0.02 | ดี, เร็ว |

> ดู pricing ล่าสุดที่: https://ai.google.dev/gemini-api/docs/pricing

---

## License

MIT License

---

## Contributing

สำหรับคำถามหรือ contribution กรุณาเปิด Issue ใน repository

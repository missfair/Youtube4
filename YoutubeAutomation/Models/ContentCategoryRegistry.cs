namespace YoutubeAutomation.Models;

public static class ContentCategoryRegistry
{
    // ===== 1. สัตว์โลกแปลก (Animal) — ค่าทั้งหมดตรงกับ hardcoded เดิม 100% =====
    public static readonly ContentCategory Animal = new()
    {
        Key = "animal",
        DisplayName = "สัตว์โลกแปลก",

        // Topic Generation
        TopicRoleDescription = "Creative Content Creator สำหรับช่อง YouTube พอดแคสต์สารคดี",
        TopicPrefixRule = "ขึ้นต้นด้วย \"ทำไม\" เพื่อสร้างความสงสัย",
        TopicExamples = new()
        {
            "ทำไมแมวกลัวแตงกวา?",
            "ทำไมดาวเหนือไม่เคลื่อนที่?",
            "ทำไมเครื่องบินไม่บินข้ามทิเบต?"
        },
        TopicBadExample = "ทำไมแมวถึงกลัวแตงกวาได้อย่างน่าประหลาดใจ?",

        // Script
        ScriptToneInstruction = "ใช้ภาษาสละสลวย น่าฟัง เหมือนสารคดีระดับโลก",
        ScriptStructureHint = "",

        // TTS
        TtsVoiceInstruction = "Read aloud in a friendly, educational, and engaging tone. Like a fun documentary narrator.",

        // Cover Image (PromptTemplates)
        CoverImageStyleDescription = "Vintage scientific illustration, comic book art style",
        CoverImageTechnique = "Bold black outlines, halftone dot shading, aged paper texture",
        CoverImageColorPalette = "Rich vibrant colors (deep blues, earth greens, warm golds, dramatic contrast)",

        // SD Local Image
        SdCartoonStylePrefix = "(rich vibrant colors:1.3), comic book art style, bold black outlines, halftone dot shading, aged paper texture, detailed ink illustration, dramatic lighting, masterpiece, best quality, ",
        SdCartoonNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, photographic, 3d render, anime, blurry, low quality, deformed, disfigured, out of frame, monochrome, grayscale, black and white",
        SdRealisticStylePrefix = "(photorealistic:1.3), professional photography, sharp focus, natural lighting, high detail, 8k uhd, DSLR quality, masterpiece, best quality, ",
        SdRealisticNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, cartoon, anime, drawing, painting, illustration, 3d render, blurry, low quality, deformed, disfigured, out of frame",
        DefaultRealisticStyle = false,

        // Cloud Image (OpenRouterService)
        CloudImageStyleDescription = "Vintage scientific illustration, 19th-century etching style",
        CloudImageColorPalette = "Muted and limited colors - sandy yellow, muted green, faded blue, sepia tones",

        // Scene Image Prompt Guidance
        ImagePromptSubjectGuidance = @"  * ถ้า text พูดถึง ""สัตว์ตัวนี้อยู่ในทะเลทราย"" → ภาพต้องเห็นสัตว์นั้นในทะเลทราย
  * ถ้า text พูดถึง ""นักวิทยาศาสตร์ค้นพบ..."" → ภาพต้องเห็นนักวิทยาศาสตร์หรือห้องแล็บ
  * ถ้า text พูดถึงข้อมูลตัวเลข/เปรียบเทียบ → ภาพต้องแสดง visual comparison",

        // BGM
        DefaultBgmMood = "curious",

        // Mood Analysis
        MoodDescriptions = new()
        {
            ["curious"] = "เรื่องน่าสงสัย ทำไมสัตว์ถึงทำแบบนี้ ความลับของธรรมชาติ ปริศนาที่ยังไม่มีคำตอบ",
            ["upbeat"] = "เรื่องสนุก น่าทึ่ง ความสามารถพิเศษของสัตว์ ข้อเท็จจริงที่น่าตื่นเต้น",
            ["gentle"] = "เรื่องธรรมชาติ วิทยาศาสตร์ อธิบายอย่างสงบ ความรู้ทั่วไป",
            ["emotional"] = "เรื่องซึ้ง ความผูกพัน สัตว์ช่วยคน มิตรภาพระหว่างสัตว์กับคน"
        },

        // YouTube
        YoutubeHashtags = "#เรื่องแปลกๆ #เรื่องแปลกแต่จริง #เรื่องแปลก #เรื่องแปลกน่ารู้ #วิทยาศาสตร์ #สารคดี #สารคดีวิทยาศาสตร์ #sciencepodcast #ความรู้รอบตัว #เรียนรู้รอบตัว",
        TopicStripWords = new() { "ทำไม", "ถึง" }
    };

    // ===== 2. ร่างกายมนุษย์ลึกลับ (Body) =====
    public static readonly ContentCategory Body = new()
    {
        Key = "body",
        DisplayName = "ร่างกายมนุษย์ลึกลับ",

        // Topic Generation
        TopicRoleDescription = "Creative Content Creator ด้านวิทยาศาสตร์ร่างกายมนุษย์ สำหรับช่อง YouTube พอดแคสต์สารคดี",
        TopicPrefixRule = "ขึ้นต้นด้วย \"ทำไม\" หรือ \"รู้หรือไม่\" เพื่อสร้างความสงสัยเกี่ยวกับร่างกาย",
        TopicExamples = new()
        {
            "ทำไมเราถึงหาว?",
            "รู้หรือไม่ ร่างกายสร้างเซลล์ใหม่กี่ล้านต่อวัน?",
            "ทำไมเราจำฝันไม่ได้?"
        },
        TopicBadExample = "ทำไมร่างกายมนุษย์ถึงมีความซับซ้อนน่าอัศจรรย์มากมาย?",

        // Script
        ScriptToneInstruction = "ใช้ภาษาเข้าใจง่าย อธิบายกลไกร่างกายอย่างน่าสนใจ เหมือนหมอเล่าให้เพื่อนฟัง",
        ScriptStructureHint = "เน้นอธิบายกลไกทางวิทยาศาสตร์ของร่างกายอย่างเข้าใจง่าย ใช้การเปรียบเทียบกับสิ่งในชีวิตประจำวัน",

        // TTS
        TtsVoiceInstruction = "Read aloud in a friendly, intimate, and educational tone. Like a knowledgeable doctor explaining fascinating body facts to a friend.",

        // Cover Image (PromptTemplates)
        CoverImageStyleDescription = "Anatomical vintage illustration, medical textbook art style",
        CoverImageTechnique = "Detailed anatomical cross-sections, fine line engraving, aged parchment texture",
        CoverImageColorPalette = "Warm natural tones (soft reds, cream whites, muted blues, earthy browns)",

        // SD Local Image
        SdCartoonStylePrefix = "(warm natural tones:1.3), anatomical vintage illustration, medical textbook art style, detailed cross-section diagram, fine line engraving, aged parchment texture, masterpiece, best quality, ",
        SdCartoonNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, photographic, 3d render, anime, blurry, low quality, deformed, disfigured, out of frame, gore, blood, graphic surgery",
        SdRealisticStylePrefix = "(photorealistic:1.3), medical photography, sharp focus, studio lighting, high detail, 8k uhd, clinical precision, masterpiece, best quality, ",
        SdRealisticNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, cartoon, anime, drawing, painting, illustration, 3d render, blurry, low quality, deformed, disfigured, out of frame, gore, blood",
        DefaultRealisticStyle = false,

        // Cloud Image (OpenRouterService)
        CloudImageStyleDescription = "Anatomical vintage illustration, 19th-century medical textbook style with fine line engraving, detailed cross-sections, and aged parchment texture",
        CloudImageColorPalette = "Warm natural tones - soft reds, cream whites, muted blues, earthy browns",

        // Scene Image Prompt Guidance
        ImagePromptSubjectGuidance = @"  * ถ้า text พูดถึง ""หัวใจเต้น..."" → ภาพต้องเห็นหัวใจหรือระบบไหลเวียนเลือด
  * ถ้า text พูดถึง ""สมองประมวลผล..."" → ภาพต้องเห็นสมองหรือระบบประสาท
  * ถ้า text พูดถึงอวัยวะภายใน → ภาพต้องแสดง anatomical cross-section ที่สวยงาม",

        // BGM
        DefaultBgmMood = "curious",

        // Mood Analysis
        MoodDescriptions = new()
        {
            ["curious"] = "เรื่องน่าสงสัย ทำไมร่างกายถึงทำแบบนี้ กลไกลึกลับที่ซ่อนอยู่",
            ["mysterious"] = "เรื่องลึกลับของร่างกาย ความลับที่วิทยาศาสตร์ยังไขไม่หมด",
            ["gentle"] = "เรื่องสุขภาพ การดูแลร่างกาย อธิบายอย่างสงบ ความรู้ทั่วไป",
            ["emotional"] = "เรื่องซึ้ง การต่อสู้ของร่างกาย ความมหัศจรรย์ของชีวิต"
        },

        // YouTube
        YoutubeHashtags = "#ร่างกายมนุษย์ #ร่างกาย #สุขภาพ #วิทยาศาสตร์ #ความลับร่างกาย #เรื่องแปลก #เรื่องแปลกน่ารู้ #สารคดี #humanbody #health",
        TopicStripWords = new() { "ทำไม", "ถึง", "รู้หรือไม่" }
    };

    // ===== 3. ประวัติศาสตร์พิสดาร (History) =====
    public static readonly ContentCategory History = new()
    {
        Key = "history",
        DisplayName = "ประวัติศาสตร์พิสดาร",

        // Topic Generation
        TopicRoleDescription = "Creative Content Creator ด้านประวัติศาสตร์โลก สำหรับช่อง YouTube พอดแคสต์สารคดี",
        TopicPrefixRule = "ขึ้นต้นด้วย \"ทำไม\" หรือ \"เรื่องจริงของ\" เพื่อสร้างความสงสัยเกี่ยวกับประวัติศาสตร์",
        TopicExamples = new()
        {
            "ทำไมกำแพงเมืองจีนถึงยาวขนาดนั้น?",
            "เรื่องจริงของไททานิก",
            "ทำไมอียิปต์สร้างพีระมิด?"
        },
        TopicBadExample = "ทำไมประวัติศาสตร์ของอารยธรรมโบราณถึงน่าสนใจมากมาย?",

        // Script
        ScriptToneInstruction = "ใช้ภาษาเล่าเรื่องดราม่า น่าติดตาม เหมือนเล่านิทานประวัติศาสตร์ ทำให้อดีตมีชีวิตขึ้นมา",
        ScriptStructureHint = "เน้นเล่าแบบมีตัวละคร มีฉาก มีเหตุการณ์ ทำให้ผู้ฟังรู้สึกเหมือนย้อนเวลาไปอยู่ในเหตุการณ์",

        // TTS
        TtsVoiceInstruction = "Read aloud in a dramatic, storytelling tone. Like a captivating history narrator bringing the past to life with tension and wonder.",

        // Cover Image (PromptTemplates)
        CoverImageStyleDescription = "Sepia-toned historical illustration, vintage map overlay art style",
        CoverImageTechnique = "Dramatic chiaroscuro lighting, detailed ink engraving, aged document texture",
        CoverImageColorPalette = "Sepia and earth tones (warm browns, faded golds, dark reds, aged paper yellows)",

        // SD Local Image
        SdCartoonStylePrefix = "(sepia tones:1.3), historical illustration, vintage map overlay, aged document texture, dramatic chiaroscuro lighting, detailed ink engraving, masterpiece, best quality, ",
        SdCartoonNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, photographic, 3d render, anime, blurry, low quality, deformed, disfigured, out of frame, modern objects, contemporary clothing",
        SdRealisticStylePrefix = "(photorealistic:1.3), historical photography, period accurate, dramatic lighting, high detail, 8k uhd, cinematic composition, masterpiece, best quality, ",
        SdRealisticNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, cartoon, anime, drawing, painting, illustration, 3d render, blurry, low quality, deformed, disfigured, out of frame, modern objects",
        DefaultRealisticStyle = false,

        // Cloud Image (OpenRouterService)
        CloudImageStyleDescription = "Sepia-toned historical illustration, 19th-century engraving style with dramatic chiaroscuro lighting, detailed ink work, and aged document texture",
        CloudImageColorPalette = "Sepia and earth tones - warm browns, faded golds, dark reds, aged paper yellows",

        // Scene Image Prompt Guidance
        ImagePromptSubjectGuidance = @"  * ถ้า text พูดถึง ""ยุคโรมัน..."" → ภาพต้องเห็นสถาปัตยกรรมหรือบรรยากาศโรมัน
  * ถ้า text พูดถึง ""สงคราม..."" → ภาพต้องเห็นสนามรบหรือทหารในชุดเกราะยุคนั้น
  * ถ้า text พูดถึงบุคคลในประวัติศาสตร์ → ภาพต้องแสดงบุคคลในบริบทและเสื้อผ้าสมัยนั้น",

        // BGM
        DefaultBgmMood = "dramatic",

        // Mood Analysis
        MoodDescriptions = new()
        {
            ["dramatic"] = "เรื่องสงคราม ความขัดแย้ง การเปลี่ยนแปลงครั้งใหญ่ เหตุการณ์ระทึกขวัญ",
            ["curious"] = "เรื่องลึกลับในประวัติศาสตร์ ปริศนาที่ยังไขไม่ได้ ทำไมถึงเกิดเหตุการณ์นี้",
            ["gentle"] = "เรื่องวัฒนธรรม ชีวิตประจำวัน การค้นพบทางโบราณคดี",
            ["emotional"] = "เรื่องซึ้ง การเสียสละ วีรบุรุษ ความรักในสมรภูมิ"
        },

        // YouTube
        YoutubeHashtags = "#ประวัติศาสตร์ #ประวัติศาสตร์โลก #เรื่องจริง #เรื่องแปลก #เรื่องแปลกน่ารู้ #สารคดี #history #historyfacts #ความรู้รอบตัว",
        TopicStripWords = new() { "ทำไม", "ถึง", "เรื่องจริงของ", "เรื่องจริง" }
    };

    // ===== 4. อวกาศและจักรวาล (Space) =====
    public static readonly ContentCategory Space = new()
    {
        Key = "space",
        DisplayName = "อวกาศและจักรวาล",

        // Topic Generation
        TopicRoleDescription = "Creative Content Creator ด้านดาราศาสตร์และอวกาศ สำหรับช่อง YouTube พอดแคสต์สารคดี",
        TopicPrefixRule = "ขึ้นต้นด้วย \"ทำไม\" หรือ \"ถ้า...?\" เพื่อสร้างจินตนาการเกี่ยวกับอวกาศ",
        TopicExamples = new()
        {
            "ทำไมอวกาศถึงมืด?",
            "ถ้าดวงอาทิตย์ดับ?",
            "ทำไมดาวเคราะห์ถึงกลม?"
        },
        TopicBadExample = "ทำไมจักรวาลถึงมีความกว้างใหญ่ไพศาลอย่างน่าอัศจรรย์?",

        // Script
        ScriptToneInstruction = "ใช้ภาษายิ่งใหญ่ ตื่นตาตื่นใจ เหมือนสารคดีอวกาศ ทำให้รู้สึกตัวเล็กในจักรวาลอันกว้างใหญ่",
        ScriptStructureHint = "เน้นขนาดและมิติที่เหลือเชื่อ ใช้การเปรียบเทียบให้เห็นภาพ เช่น ถ้าดวงอาทิตย์เท่าลูกบาสเกตบอล...",

        // TTS
        TtsVoiceInstruction = "Read aloud in a grand, awe-inspiring tone. Like a cosmic documentary narrator exploring the wonders of the universe with wonder and reverence.",

        // Cover Image (PromptTemplates)
        CoverImageStyleDescription = "Cosmic photorealistic illustration, space art style",
        CoverImageTechnique = "Dramatic volumetric lighting, nebula colors, starfield background",
        CoverImageColorPalette = "Deep cosmic colors (midnight blues, nebula purples, starlight whites, solar golds)",

        // SD Local Image
        SdCartoonStylePrefix = "(cosmic colors:1.3), space art illustration, nebula painting style, starfield background, dramatic volumetric lighting, detailed digital art, masterpiece, best quality, ",
        SdCartoonNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, photographic, 3d render, anime, blurry, low quality, deformed, disfigured, out of frame, earthly landscape, indoor scene",
        SdRealisticStylePrefix = "(photorealistic:1.3), cosmic photography, NASA quality, nebula colors, starfield background, dramatic volumetric lighting, 8k uhd, masterpiece, best quality, ",
        SdRealisticNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, cartoon, anime, drawing, painting, illustration, 3d render, blurry, low quality, deformed, disfigured, out of frame",
        DefaultRealisticStyle = true,

        // Cloud Image (OpenRouterService)
        CloudImageStyleDescription = "Cosmic photorealistic art, space photography style with dramatic volumetric lighting, nebula colors, and starfield background",
        CloudImageColorPalette = "Deep cosmic colors - midnight blues, nebula purples, starlight whites, solar golds",

        // Scene Image Prompt Guidance
        ImagePromptSubjectGuidance = @"  * ถ้า text พูดถึง ""ดาวเคราะห์..."" → ภาพต้องเห็นดาวเคราะห์ลอยอยู่ในอวกาศ
  * ถ้า text พูดถึง ""หลุมดำ..."" → ภาพต้องเห็นหลุมดำกับ accretion disk ที่เรืองแสง
  * ถ้า text พูดถึงนักบินอวกาศ → ภาพต้องแสดงนักบินอวกาศในสถานีหรือบนพื้นผิวดาว",

        // BGM
        DefaultBgmMood = "epic",

        // Mood Analysis
        MoodDescriptions = new()
        {
            ["epic"] = "เรื่องยิ่งใหญ่ อลังการ ขนาดมหึมาของจักรวาล ดวงดาวระเบิด กาแลกซีชนกัน",
            ["curious"] = "เรื่องลึกลับในอวกาศ ปริศนาจักรวาล ทำไมอวกาศถึงเป็นแบบนี้",
            ["gentle"] = "เรื่องดาราศาสตร์พื้นฐาน ระบบสุริยะ ดวงดาวบนท้องฟ้า อธิบายอย่างสงบ",
            ["emotional"] = "เรื่องซึ้ง ความโดดเดี่ยวในอวกาศ มนุษย์กับจักรวาล ความฝันของนักสำรวจ"
        },

        // YouTube
        YoutubeHashtags = "#อวกาศ #จักรวาล #ดาราศาสตร์ #ระบบสุริยะ #เรื่องแปลก #เรื่องแปลกน่ารู้ #สารคดี #space #universe #astronomy",
        TopicStripWords = new() { "ทำไม", "ถึง", "ถ้า" }
    };

    // ===== หมวดหมู่ในอนาคต =====
    // TODO: public static readonly ContentCategory Food = new() { Key = "food", DisplayName = "อาหารแปลก น่ารู้", ... };
    // TODO: public static readonly ContentCategory Tech = new() { Key = "tech", DisplayName = "เทคโนโลยีเปลี่ยนโลก", ... };
    // TODO: public static readonly ContentCategory Psychology = new() { Key = "psychology", DisplayName = "จิตวิทยาพิศวง", ... };
    // TODO: public static readonly ContentCategory Nature = new() { Key = "nature", DisplayName = "ปรากฏการณ์ธรรมชาติ", ... };

    public static readonly ContentCategory[] All = { Animal, Body, History, Space };

    public static ContentCategory GetByKey(string? key)
        => All.FirstOrDefault(c => c.Key.Equals(key ?? "", StringComparison.OrdinalIgnoreCase))
           ?? Animal; // default = animal (backwards compatible)
}

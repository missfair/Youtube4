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
        DefaultRealisticStyle = true,

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

    // ===== 2. สุขภาพดี มีสุข (Body/Health Podcast) =====
    public static readonly ContentCategory Body = new()
    {
        Key = "body",
        DisplayName = "สุขภาพดี มีสุข",

        // Topic Generation - Health & Wellness Focus
        TopicRoleDescription = "นักเขียนเนื้อหาพอดแคสต์สุขภาพ สำหรับผู้ชมสูงอายุที่รักสุขภาพ เน้นคำแนะนำเชิงปฏิบัติและการดูแลตนเองอย่างถูกต้อง",
        TopicPrefixRule = "ขึ้นต้นด้วยคำถามเกี่ยวกับสุขภาพที่ผู้ชมสงสัย เช่น \"ทำไม...\" \"วิธีไหน...\" \"อายุมากควร...\"",
        TopicExamples = new()
        {
            "ทำไมคนอายุมากต้องดื่มน้ำเยอะ?",
            "วิธีไหนดูแลกระดูกให้แข็งแรง?",
            "อายุมากควรออกกำลังกายอย่างไร?",
            "ทำไมนอนหลับยากเมื่ออายุมากขึ้น?",
            "วิธีป้องกันความจำเสื่อมตั้งแต่อายุ 50?"
        },
        TopicBadExample = "ทำไมร่างกายมนุษย์ถึงมีความซับซ้อนน่าอัศจรรย์มากมาย? (ยาวเกิน ไม่เน้นสุขภาพ)",

        // Script - Friendly Podcast Tone for Older Audience
        ScriptToneInstruction = "ใช้ภาษาง่าย เป็นกันเอง เหมือนพูดคุยกับญาติผู้ใหญ่ ให้คำแนะนำเชิงปฏิบัติ ไม่ใช้คำศัพท์การแพทย์ซับซ้อน เน้นการดูแลสุขภาพในชีวิตประจำวัน",
        ScriptStructureHint = "เริ่มจากปัญหาที่คนวัยกลางคนเจอ → อธิบายสาเหตุอย่างเข้าใจง่าย → เสนอวิธีแก้ไขหรือป้องกันที่ทำได้จริง → สรุปด้วยคำแนะนำง่ายๆ",

        // TTS - Warm, Reassuring Voice
        TtsVoiceInstruction = "Read aloud in a warm, friendly, and reassuring tone. Like a caring health advisor talking to an older relative. Speak clearly and at a moderate pace. Be encouraging and positive.",

        // Cover Image - Healthy Lifestyle Focus
        CoverImageStyleDescription = "Warm lifestyle photography, health and wellness magazine cover style",
        CoverImageTechnique = "Bright natural lighting, optimistic mood, relatable healthy lifestyle imagery (exercise, nutrition, wellness)",
        CoverImageColorPalette = "Fresh warm tones (vibrant greens, warm oranges, clean whites, sky blues) - energetic but not aggressive",

        // SD Local Image - Healthy Lifestyle Illustrations
        SdCartoonStylePrefix = "(warm bright tones:1.3), health lifestyle illustration, magazine cover art style, optimistic wellness imagery, clean modern design, vibrant colors, masterpiece, best quality, ",
        SdCartoonNegativePrompt = "medical diagram, anatomy, surgery, hospital, clinical, dark, gloomy, scary, illness, disease, text, letters, words, numbers, watermark, signature, logo, photographic, 3d render, anime, blurry, low quality",
        SdRealisticStylePrefix = "(photorealistic:1.3), lifestyle photography, natural lighting, healthy living, wellness magazine style, positive mood, sharp focus, 8k uhd, masterpiece, best quality, ",
        SdRealisticNegativePrompt = "medical facility, hospital, clinical, x-ray, surgery, illness, text, letters, words, numbers, watermark, signature, logo, cartoon, anime, drawing, painting, illustration, 3d render, blurry, low quality",
        DefaultRealisticStyle = true,

        // Cloud Image - Healthy Lifestyle Focus
        CloudImageStyleDescription = "Warm lifestyle photography, health magazine editorial style with bright natural lighting, showing active seniors, healthy food, outdoor activities, and wellness practices",
        CloudImageColorPalette = "Fresh energetic tones - vibrant greens (vegetables, nature), warm oranges (vitality), clean whites (purity), sky blues (calm wellness)",

        // Scene Image Prompt Guidance
        ImagePromptSubjectGuidance = @"  * ถ้า text พูดถึง ""ดื่มน้ำ..."" → ภาพต้องเห็นน้ำใส แก้วน้ำ หรือการดื่มน้ำ
  * ถ้า text พูดถึง ""ออกกำลังกาย..."" → ภาพต้องเห็นคนสูงอายุกำลังเคลื่อนไหว เดิน หรือยืดเหยียด
  * ถ้า text พูดถึงอาหาร → ภาพต้องแสดงผัก ผลไม้ อาหารสุขภาพ
  * ถ้า text พูดถึงการนอน → ภาพบรรยากาศผ่อนคลาย เตียงนอนสบาย ห้องสงบ
  * เน้นภาพให้ดูสดใส มีชีวิตชีวา เป็นบวก ไม่มืดหม่น",

        // BGM - Gentle and Uplifting
        DefaultBgmMood = "gentle",

        // Mood Analysis
        MoodDescriptions = new()
        {
            ["gentle"] = "เรื่องสุขภาพทั่วไป คำแนะนำเบื้องต้น อธิบายอย่างอ่อนโยน ให้กำลังใจ",
            ["upbeat"] = "เรื่องการออกกำลังกาย กิจกรรมสนุก พลังบวก สร้างแรงบันดาลใจ",
            ["curious"] = "เรื่องน่ารู้เกี่ยวกับร่างกาย ข้อมูลสุขภาพใหม่ การค้นพบทางการแพทย์",
            ["emotional"] = "เรื่องการดูแลผู้สูงอายุ ความสัมพันธ์ครอบครัว ความผูกพันระหว่างวัย"
        },

        // YouTube Hashtags - Health & Wellness
        YoutubeHashtags = "#สุขภาพดี #คนวัยกลางคน #ผู้สูงอายุ #สุขภาพผู้สูงอายุ #เคล็ดลับสุขภาพ #พอดแคสต์สุขภาพ #ดูแลสุขภาพ #wellness #health #healthyaging",
        TopicStripWords = new() { "ทำไม", "ถึง", "วิธีไหน", "อายุมากควร" }
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
        DefaultRealisticStyle = true,

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

    // ===== 5. อาหารรสชาติ (Food) =====
    public static readonly ContentCategory Food = new()
    {
        Key = "food",
        DisplayName = "อาหารรสชาติ",

        // Topic Generation
        TopicRoleDescription = "Creative Content Creator ด้านอาหารและวิทยาศาสตร์การทำอาหาร สำหรับช่อง YouTube พอดแคสต์สารคดี",
        TopicPrefixRule = "ขึ้นต้นด้วย \"ทำไม\" เพื่อสร้างความสงสัยเกี่ยวกับอาหาร",
        TopicExamples = new()
        {
            "ทำไมหัวหอมทำให้น้ำตาไหล?",
            "ทำไมข้าวเหนียวถึงเหนียว?",
            "ทำไมอาหารเผ็ดถึงทำให้เสพติด?"
        },
        TopicBadExample = "ทำไมอาหารไทยถึงเป็นอาหารที่มีรสชาติอร่อยและหลากหลายมากที่สุด?",

        // Script
        ScriptToneInstruction = "ใช้ภาษาสนุก เป็นกันเอง เหมือนรายการอาหารสารคดี ทำให้ผู้ฟังอยากลองทำตาม",
        ScriptStructureHint = "เริ่มจากคำถามน่าสงสัยเกี่ยวกับอาหาร → อธิบายวิทยาศาสตร์เบื้องหลัง → เชื่อมโยงกับชีวิตประจำวัน → สรุปด้วยเกร็ดน่ารู้",

        // TTS
        TtsVoiceInstruction = "Read aloud in a fun, enthusiastic, and warm tone. Like a passionate food documentary narrator who loves sharing culinary secrets.",

        // Cover Image
        CoverImageStyleDescription = "Warm food photography, vintage cookbook cover art style",
        CoverImageTechnique = "Appetizing food styling, warm golden lighting, rustic wood texture background",
        CoverImageColorPalette = "Warm appetizing tones (golden browns, rich reds, fresh greens, creamy whites)",

        // SD Local Image
        SdCartoonStylePrefix = "(warm golden tones:1.3), food illustration art style, appetizing food styling, vintage cookbook, warm lighting, detailed watercolor, masterpiece, best quality, ",
        SdCartoonNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, photographic, 3d render, anime, blurry, low quality, deformed, disfigured, out of frame, unappetizing, rotten",
        SdRealisticStylePrefix = "(photorealistic:1.3), professional food photography, appetizing styling, warm golden lighting, shallow depth of field, 8k uhd, DSLR quality, masterpiece, best quality, ",
        SdRealisticNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, cartoon, anime, drawing, painting, illustration, 3d render, blurry, low quality, deformed, disfigured, out of frame, unappetizing",
        DefaultRealisticStyle = true,

        // Cloud Image
        CloudImageStyleDescription = "Professional food photography, warm golden lighting with appetizing food styling, rustic background, shallow depth of field",
        CloudImageColorPalette = "Warm appetizing tones - golden browns, rich reds, fresh greens, creamy whites",

        // Scene Image Prompt Guidance
        ImagePromptSubjectGuidance = @"  * ถ้า text พูดถึง ""การทอด..."" → ภาพต้องเห็นกระทะร้อนกับน้ำมันที่เดือด
  * ถ้า text พูดถึง ""สารเคมีในอาหาร..."" → ภาพต้องเห็นส่วนผสมในครัวหรือโมเลกุล
  * ถ้า text พูดถึงวัตถุดิบ → ภาพต้องแสดงวัตถุดิบสดใหม่บนเขียง
  * เน้นภาพให้ดูน่ากิน อบอุ่น มีชีวิตชีวา",

        // BGM
        DefaultBgmMood = "playful",

        // Mood Analysis
        MoodDescriptions = new()
        {
            ["playful"] = "เรื่องสนุก น่ารู้ เกร็ดอาหารแปลกๆ วิธีทำอาหารสุดครีเอทีฟ",
            ["curious"] = "เรื่องน่าสงสัย วิทยาศาสตร์เบื้องหลังอาหาร ทำไมรสชาติถึงเป็นแบบนี้",
            ["upbeat"] = "เรื่องอาหารที่ทำให้มีความสุข เทศกาลอาหาร street food",
            ["gentle"] = "เรื่องอาหารพื้นบ้าน ประวัติอาหาร วัฒนธรรมการกิน"
        },

        // YouTube
        YoutubeHashtags = "#อาหาร #วิทยาศาสตร์อาหาร #อาหารน่ารู้ #เรื่องแปลกอาหาร #สารคดีอาหาร #foodscience #foodfacts #ความรู้รอบตัว #เรื่องแปลกแต่จริง",
        TopicStripWords = new() { "ทำไม", "ถึง" }
    };

    // ===== 6. เทคโนโลยีเปลี่ยนโลก (Technology) =====
    public static readonly ContentCategory Technology = new()
    {
        Key = "technology",
        DisplayName = "เทคโนโลยีเปลี่ยนโลก",

        // Topic Generation
        TopicRoleDescription = "Creative Content Creator ด้านเทคโนโลยีและนวัตกรรม สำหรับช่อง YouTube พอดแคสต์สารคดี",
        TopicPrefixRule = "ขึ้นต้นด้วย \"ทำไม\" หรือ \"ถ้า...?\" เพื่อสร้างความสงสัยเกี่ยวกับเทคโนโลยี",
        TopicExamples = new()
        {
            "ทำไม AI ถึงฉลาดกว่ามนุษย์บางอย่าง?",
            "ถ้าอินเทอร์เน็ตดับทั่วโลก?",
            "ทำไมแบตเตอรี่มือถือถึงเสื่อมเร็ว?"
        },
        TopicBadExample = "ทำไมเทคโนโลยีสมัยใหม่ถึงมีความก้าวหน้าอย่างน่าทึ่งมากมาย?",

        // Script
        ScriptToneInstruction = "ใช้ภาษาตื่นเต้น ทันสมัย เข้าใจง่าย เหมือนสารคดีเทคโนโลยี อธิบายเรื่องซับซ้อนให้คนทั่วไปเข้าใจ",
        ScriptStructureHint = "เริ่มจากปรากฏการณ์ที่เห็นในชีวิตจริง → อธิบายเทคโนโลยีเบื้องหลัง → แสดงผลกระทบต่อโลก → จบด้วยอนาคตที่น่าตื่นเต้น",

        // TTS
        TtsVoiceInstruction = "Read aloud in an exciting, modern, and energetic tone. Like a tech documentary narrator who is genuinely amazed by innovation and makes complex topics accessible.",

        // Cover Image
        CoverImageStyleDescription = "Futuristic neon-lit technology illustration, sci-fi magazine cover style",
        CoverImageTechnique = "Neon glow effects, circuit board patterns, holographic elements, dark background with vibrant accents",
        CoverImageColorPalette = "Futuristic neon colors (electric blue, neon cyan, deep purple, bright white accents on dark background)",

        // SD Local Image
        SdCartoonStylePrefix = "(neon glow:1.3), futuristic technology illustration, sci-fi art style, circuit board patterns, holographic elements, dark background, vibrant neon accents, masterpiece, best quality, ",
        SdCartoonNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, photographic, 3d render, anime, blurry, low quality, deformed, disfigured, out of frame, medieval, ancient, rustic",
        SdRealisticStylePrefix = "(photorealistic:1.3), technology photography, futuristic lighting, neon accents, modern clean design, sharp focus, 8k uhd, masterpiece, best quality, ",
        SdRealisticNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, cartoon, anime, drawing, painting, illustration, 3d render, blurry, low quality, deformed, disfigured, out of frame, medieval, ancient",
        DefaultRealisticStyle = true,

        // Cloud Image
        CloudImageStyleDescription = "Futuristic technology photography, neon-lit with holographic elements, modern clean design, dark background with vibrant accent lighting",
        CloudImageColorPalette = "Futuristic neon colors - electric blue, neon cyan, deep purple, bright white accents on dark background",

        // Scene Image Prompt Guidance
        ImagePromptSubjectGuidance = @"  * ถ้า text พูดถึง ""AI..."" → ภาพต้องเห็นหุ่นยนต์ ชิป หรือ neural network visualization
  * ถ้า text พูดถึง ""สมาร์ทโฟน..."" → ภาพต้องเห็นอุปกรณ์เทคโนโลยีทันสมัย
  * ถ้า text พูดถึงข้อมูล/อินเทอร์เน็ต → ภาพต้องแสดง data visualization หรือ server room
  * เน้นภาพให้ดูล้ำสมัย มีแสงนีออน บรรยากาศแห่งอนาคต",

        // BGM
        DefaultBgmMood = "curious",

        // Mood Analysis
        MoodDescriptions = new()
        {
            ["curious"] = "เรื่องน่าสงสัย เทคโนโลยีทำงานอย่างไร ทำไมถึงเป็นแบบนี้",
            ["upbeat"] = "เรื่อง gadget สนุก นวัตกรรมเจ๋ง เทคโนโลยีที่ทำให้ชีวิตดีขึ้น",
            ["mysterious"] = "เรื่องลึกลับในโลกดิจิทัล ไซเบอร์ซีเคียวริตี้ dark web AI ที่น่ากลัว",
            ["epic"] = "เรื่องยิ่งใหญ่ เทคโนโลยีเปลี่ยนโลก การปฏิวัติอุตสาหกรรม"
        },

        // YouTube
        YoutubeHashtags = "#เทคโนโลยี #AI #นวัตกรรม #เรื่องแปลก #เรื่องแปลกน่ารู้ #สารคดี #technology #techpodcast #ความรู้รอบตัว #อนาคต",
        TopicStripWords = new() { "ทำไม", "ถึง", "ถ้า" }
    };

    // ===== 7. จิตวิทยาพิศวง (Psychology) =====
    public static readonly ContentCategory Psychology = new()
    {
        Key = "psychology",
        DisplayName = "จิตวิทยาพิศวง",

        // Topic Generation
        TopicRoleDescription = "Creative Content Creator ด้านจิตวิทยาและพฤติกรรมมนุษย์ สำหรับช่อง YouTube พอดแคสต์สารคดี",
        TopicPrefixRule = "ขึ้นต้นด้วย \"ทำไมคนถึง...\" หรือ \"ทำไมสมองของเรา...\" เพื่อสร้างความสงสัยเกี่ยวกับจิตใจ",
        TopicExamples = new()
        {
            "ทำไมคนถึงกลัวการพูดหน้าห้อง?",
            "ทำไมสมองของเราถูกหลอกง่าย?",
            "ทำไมคนถึงชอบเลื่อนมือถือไม่หยุด?"
        },
        TopicBadExample = "ทำไมจิตวิทยามนุษย์ถึงมีความซับซ้อนและน่าพิศวงอย่างมากมาย?",

        // Script
        ScriptToneInstruction = "ใช้ภาษาลึกลับ น่าคิด ชวนตั้งคำถาม เหมือนสารคดีจิตวิทยา ทำให้ผู้ฟังสำรวจตัวเอง",
        ScriptStructureHint = "เริ่มจากพฤติกรรมที่ทุกคนเคยเจอ → อธิบายทฤษฎีจิตวิทยาเบื้องหลัง → ยกตัวอย่างการทดลองที่น่าสนใจ → สรุปด้วยข้อคิดที่นำไปใช้ได้จริง",

        // TTS
        TtsVoiceInstruction = "Read aloud in a thoughtful, intriguing, and slightly mysterious tone. Like a psychology documentary narrator who draws listeners into exploring their own minds.",

        // Cover Image
        CoverImageStyleDescription = "Surreal mind illustration, dark moody psychology art style",
        CoverImageTechnique = "Surreal double exposure effects, brain imagery, optical illusions, dramatic shadows",
        CoverImageColorPalette = "Dark moody tones (deep indigo, midnight purple, warm amber accents, muted teal)",

        // SD Local Image
        SdCartoonStylePrefix = "(dark moody tones:1.3), surreal psychology illustration, mind art style, double exposure effect, brain imagery, optical illusion, dramatic shadows, masterpiece, best quality, ",
        SdCartoonNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, photographic, 3d render, anime, blurry, low quality, deformed, disfigured, out of frame, cheerful, bright colors",
        SdRealisticStylePrefix = "(photorealistic:1.3), conceptual psychology photography, surreal double exposure, moody lighting, dramatic shadows, 8k uhd, cinematic composition, masterpiece, best quality, ",
        SdRealisticNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, cartoon, anime, drawing, painting, illustration, 3d render, blurry, low quality, deformed, disfigured, out of frame",
        DefaultRealisticStyle = true,

        // Cloud Image
        CloudImageStyleDescription = "Surreal conceptual photography, dark moody psychology art with double exposure effects, brain imagery, optical illusions, and dramatic shadows",
        CloudImageColorPalette = "Dark moody tones - deep indigo, midnight purple, warm amber accents, muted teal",

        // Scene Image Prompt Guidance
        ImagePromptSubjectGuidance = @"  * ถ้า text พูดถึง ""สมอง..."" → ภาพต้องเห็นสมองหรือ neural pathway
  * ถ้า text พูดถึง ""ความกลัว..."" → ภาพต้องแสดงบรรยากาศมืดมิด เงา หรือใบหน้าที่กลัว
  * ถ้า text พูดถึงการทดลอง → ภาพต้องเห็นห้องทดลองจิตวิทยาหรือคนกำลังถูกทดสอบ
  * เน้นภาพให้ดูลึกลับ ชวนคิด บรรยากาศ moody",

        // BGM
        DefaultBgmMood = "mysterious",

        // Mood Analysis
        MoodDescriptions = new()
        {
            ["mysterious"] = "เรื่องลึกลับของจิตใจ จิตใต้สำนึก ความลับของสมอง ปรากฏการณ์แปลกๆ",
            ["curious"] = "เรื่องน่าสงสัย ทำไมคนถึงทำแบบนี้ cognitive bias การตัดสินใจ",
            ["gentle"] = "เรื่องจิตวิทยาเชิงบวก การดูแลสุขภาพจิต ความสัมพันธ์ที่ดี",
            ["emotional"] = "เรื่องซึ้ง ความผูกพัน ความรัก การสูญเสีย อารมณ์ที่ซับซ้อน"
        },

        // YouTube
        YoutubeHashtags = "#จิตวิทยา #พฤติกรรมมนุษย์ #สมอง #เรื่องแปลก #เรื่องแปลกน่ารู้ #สารคดี #psychology #mindblown #ความรู้รอบตัว #ความคิด",
        TopicStripWords = new() { "ทำไม", "ถึง", "ทำไมคนถึง", "ทำไมสมองของเรา" }
    };

    // ===== 8. ธรรมชาติมหัศจรรย์ (Nature) =====
    public static readonly ContentCategory Nature = new()
    {
        Key = "nature",
        DisplayName = "ธรรมชาติมหัศจรรย์",

        // Topic Generation
        TopicRoleDescription = "Creative Content Creator ด้านธรรมชาติและสิ่งแวดล้อม สำหรับช่อง YouTube พอดแคสต์สารคดี",
        TopicPrefixRule = "ขึ้นต้นด้วย \"ทำไม\" หรือ \"ถ้า...?\" เพื่อสร้างความสงสัยเกี่ยวกับธรรมชาติ",
        TopicExamples = new()
        {
            "ทำไมท้องฟ้าถึงเป็นสีฟ้า?",
            "ถ้าป่าอะเมซอนหายไป?",
            "ทำไมภูเขาไฟถึงระเบิด?"
        },
        TopicBadExample = "ทำไมธรรมชาติถึงมีความสวยงามและน่าอัศจรรย์อย่างมากมาย?",

        // Script
        ScriptToneInstruction = "ใช้ภาษาสงบ สง่างาม เหมือนสารคดีธรรมชาติระดับ Netflix ทำให้รู้สึกเชื่อมต่อกับธรรมชาติ",
        ScriptStructureHint = "เริ่มจากปรากฏการณ์ธรรมชาติที่น่าทึ่ง → อธิบายกลไกทางวิทยาศาสตร์ → แสดงผลกระทบต่อระบบนิเวศ → สรุปด้วยความสำคัญต่อมนุษย์",

        // TTS
        TtsVoiceInstruction = "Read aloud in a serene, majestic, and contemplative tone. Like a nature documentary narrator from Netflix, filled with wonder and respect for the natural world.",

        // Cover Image
        CoverImageStyleDescription = "Stunning landscape photography, National Geographic magazine cover style",
        CoverImageTechnique = "Golden hour lighting, dramatic wide angle, vivid natural colors, awe-inspiring scale",
        CoverImageColorPalette = "Natural earth tones (emerald greens, ocean blues, sunset oranges, mountain grays, sky whites)",

        // SD Local Image
        SdCartoonStylePrefix = "(natural earth tones:1.3), nature illustration art style, landscape painting, golden hour lighting, vivid natural colors, panoramic view, masterpiece, best quality, ",
        SdCartoonNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, photographic, 3d render, anime, blurry, low quality, deformed, disfigured, out of frame, urban, city, building",
        SdRealisticStylePrefix = "(photorealistic:1.3), nature photography, National Geographic quality, golden hour lighting, dramatic wide angle, vivid natural colors, 8k uhd, masterpiece, best quality, ",
        SdRealisticNegativePrompt = "text, letters, words, numbers, watermark, signature, logo, cartoon, anime, drawing, painting, illustration, 3d render, blurry, low quality, deformed, disfigured, out of frame, urban, city",
        DefaultRealisticStyle = true,

        // Cloud Image
        CloudImageStyleDescription = "Stunning nature photography, National Geographic style with golden hour lighting, dramatic wide angle, vivid natural colors, and awe-inspiring scale",
        CloudImageColorPalette = "Natural earth tones - emerald greens, ocean blues, sunset oranges, mountain grays, sky whites",

        // Scene Image Prompt Guidance
        ImagePromptSubjectGuidance = @"  * ถ้า text พูดถึง ""ภูเขาไฟ..."" → ภาพต้องเห็นภูเขาไฟกับลาวาหรือควัน
  * ถ้า text พูดถึง ""มหาสมุทร..."" → ภาพต้องเห็นทะเลกว้างใหญ่หรือสิ่งมีชีวิตใต้น้ำ
  * ถ้า text พูดถึงป่า → ภาพต้องแสดงป่าทึบ ต้นไม้ใหญ่ แสงลอดผ่านใบไม้
  * เน้นภาพให้ดูยิ่งใหญ่ สง่างาม เชื่อมต่อกับธรรมชาติ",

        // BGM
        DefaultBgmMood = "gentle",

        // Mood Analysis
        MoodDescriptions = new()
        {
            ["gentle"] = "เรื่องธรรมชาติอ่อนโยน ป่าไม้ ลำธาร ระบบนิเวศ อธิบายอย่างสงบ",
            ["epic"] = "เรื่องยิ่งใหญ่ ภูเขาไฟระเบิด สึนามิ พายุ ปรากฏการณ์ทรงพลัง",
            ["curious"] = "เรื่องน่าสงสัย ทำไมธรรมชาติถึงเป็นแบบนี้ ปริศนาทางวิทยาศาสตร์",
            ["emotional"] = "เรื่องซึ้ง การอนุรักษ์ สัตว์ใกล้สูญพันธุ์ ความสวยงามที่กำลังหายไป"
        },

        // YouTube
        YoutubeHashtags = "#ธรรมชาติ #สิ่งแวดล้อม #ปรากฏการณ์ธรรมชาติ #เรื่องแปลก #เรื่องแปลกน่ารู้ #สารคดี #nature #environment #ความรู้รอบตัว #โลกของเรา",
        TopicStripWords = new() { "ทำไม", "ถึง", "ถ้า" }
    };

    public static readonly ContentCategory[] All = { Animal, Body, History, Space, Food, Technology, Psychology, Nature };

    public static ContentCategory GetByKey(string? key)
        => All.FirstOrDefault(c => c.Key.Equals(key ?? "", StringComparison.OrdinalIgnoreCase))
           ?? Animal; // default = animal (backwards compatible)
}

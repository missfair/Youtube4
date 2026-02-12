using YoutubeAutomation.Models;

namespace YoutubeAutomation.Prompts;

public static class PromptTemplates
{
    // === Backward-compatible wrapper (ผลลัพธ์เหมือนเดิม 100%) ===
    public static string GetTopicGenerationPrompt(string subject)
        => GetTopicGenerationPrompt(subject, ContentCategoryRegistry.Animal);

    public static string GetTopicGenerationPrompt(string subject, ContentCategory category)
    {
        var examples = string.Join("\n", category.TopicExamples.Select(e => $"- \"{e}\""));

        return $@"สวมบทบาทเป็น {category.TopicRoleDescription} ""เรื่องแปลก น่ารู้""

ภารกิจของคุณ: เสนอหัวข้อเกี่ยวกับ ""{subject}"" มา 5 หัวข้อ โดยมีเงื่อนไขดังนี้:

**กฎสำคัญสำหรับหัวข้อ:**
1. หัวข้อต้อง **สั้นกระชับ** ไม่เกิน 6-8 คำ (เหมาะกับปก YouTube)
2. {category.TopicPrefixRule}
3. ใช้คำที่ดึงดูดใจ น่าคลิก แต่ไม่หลอกลวง
4. ตัดคำฟุ่มเฟือยออก เช่น ""ได้อย่างไร"" ""ที่น่าทึ่ง"" ""กันแน่""

**ตัวอย่างหัวข้อที่ดี:**
{examples}

**ตัวอย่างหัวข้อที่ยาวเกินไป (ไม่ต้องการ):**
- ""{category.TopicBadExample}"" ❌

เนื้อหาต้องเข้มข้นพอขยายเป็นวิดีโอ 15 นาทีได้

ตอบในรูปแบบ:
1. [หัวข้อสั้นๆ]
   คำอธิบาย: [1 ประโยคสั้นๆ]

2. [หัวข้อสั้นๆ]
   คำอธิบาย: [1 ประโยคสั้นๆ]

(ต่อจนครบ 5 หัวข้อ)";
    }

    // === Backward-compatible wrapper ===
    public static string GetScriptGenerationPrompt(string topic, int partNumber, List<string>? previousParts = null)
        => GetScriptGenerationPrompt(topic, partNumber, previousParts, ContentCategoryRegistry.Animal);

    public static string GetScriptGenerationPrompt(string topic, int partNumber, List<string>? previousParts, ContentCategory category)
    {
        var (partDescription, transitionRule) = partNumber switch
        {
            1 => (
                "เกริ่นนำ - สร้างความสนใจ ตั้งคำถามที่ทำให้ผู้ฟังอยากรู้ต่อ บอกเล่าปูมหลังของเรื่อง",
                @"**กฎการเปิด-ปิดส่วนที่ 1:**
- เปิดด้วย: ""สวัสดีครับ ยินดีต้อนรับสู่ เรื่องแปลก น่ารู้..."" (แนะนำรายการและหัวข้อ)
- ห้ามปิดด้วย ""พบกันใหม่"" หรือ ""สวัสดีครับ"" เด็ดขาด
- ปิดด้วยคำเชื่อมเช่น: ""แต่นี่เป็นเพียงจุดเริ่มต้นเท่านั้น..."" หรือ ""เรื่องราวกำลังจะน่าสนใจยิ่งขึ้น..."" หรือ ""มาดูกันต่อว่าเกิดอะไรขึ้น..."""
            ),
            2 => (
                "เนื้อหาหลัก - ข้อมูลเชิงลึก หลักฐานทางวิทยาศาสตร์หรือประวัติศาสตร์ เรื่องราวที่น่าสนใจ",
                @"**กฎการเปิด-ปิดส่วนที่ 2:**
- ห้ามเปิดด้วย ""สวัสดี"" เด็ดขาด (เพราะเป็นการต่อเนื่องจากส่วนที่ 1)
- เปิดด้วยคำเชื่อมเช่น: ""จากที่เราได้เล่าไป..."" หรือ ""ทีนี้ มาดูรายละเอียดกันต่อ..."" หรือ เริ่มต้นด้วยเนื้อหาต่อเนื่องเลย
- ห้ามปิดด้วย ""พบกันใหม่"" หรือ ""สวัสดีครับ"" เด็ดขาด
- ปิดด้วยคำเชื่อมเช่น: ""แต่ยังมีอีกสิ่งหนึ่งที่ต้องพิจารณา..."" หรือ ""สิ่งที่เกิดขึ้นต่อมานั้นน่าทึ่งยิ่งกว่า..."""
            ),
            3 => (
                "สรุป - สรุปประเด็นสำคัญ ทิ้งคำถามให้คิดต่อ ปิดท้ายอย่างน่าจดจำ",
                @"**กฎการเปิด-ปิดส่วนที่ 3:**
- ห้ามเปิดด้วย ""สวัสดี"" เด็ดขาด (เพราะเป็นการต่อเนื่องจากส่วนที่ 2)
- เปิดด้วยคำเชื่อมเช่น: ""มาถึงตอนนี้..."" หรือ ""สิ่งที่เราได้เรียนรู้คือ..."" หรือ เริ่มสรุปประเด็นเลย
- **ปิดท้ายด้วย:** ""หวังว่าเรื่องราววันนี้จะให้ความรู้และความบันเทิง แล้วพบกันใหม่ในตอนหน้า กับ เรื่องแปลก น่ารู้ สวัสดีครับ"""
            ),
            _ => ("เนื้อหา", "")
        };

        var previousPartsContext = "";
        if (previousParts != null && previousParts.Count > 0)
        {
            previousPartsContext = "\n\n=== บทที่เขียนไปแล้ว (ใช้เป็น context เพื่อให้เนื้อหาต่อเนื่องกัน ไม่ซ้ำกัน) ===\n";
            for (int i = 0; i < previousParts.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(previousParts[i]))
                {
                    previousPartsContext += $"\n--- ส่วนที่ {i + 1} ---\n{previousParts[i]}\n";
                }
            }
            previousPartsContext += "\n=== จบ context ===\n";
        }

        var structureHint = string.IsNullOrWhiteSpace(category.ScriptStructureHint)
            ? ""
            : $"\n- {category.ScriptStructureHint}";

        return $@"สร้างบทพูดสำหรับวิดีโอ YouTube podcast สารคดี ช่อง ""เรื่องแปลก น่ารู้""

หัวข้อ: {topic}
นี่คือส่วนที่ {partNumber} จาก 3 ส่วน (ทั้ง 3 ส่วนจะรวมเป็นวิดีโอเดียวกัน ความยาวรวมประมาณ 12-15 นาที)
ส่วนนี้คือ: {partDescription}

{transitionRule}
{previousPartsContext}
กรุณาเขียนบทพูดภาษาไทยที่:
- มีความยาวประมาณ 4-5 นาทีเมื่ออ่านออกเสียง (ประมาณ 1200 คำ — สำคัญมาก: TTS ภาษาไทยอ่านเร็วกว่าคนจริง ต้องเขียนให้ยาวเพียงพอ)
- {category.ScriptToneInstruction}
- มีจังหวะหยุดพัก [Pause] ในจุดที่เหมาะสม
- ทำให้ผู้ฟังสงสัยและอยากฟังต่อ
- ไม่น่าเบื่อ มีข้อมูลที่น่าสนใจ{structureHint}
- **สำคัญ: ปฏิบัติตามกฎการเปิด-ปิดอย่างเคร่งครัด**
- **สำคัญ: ต้องไม่ซ้ำกับเนื้อหาที่เขียนไปแล้วในส่วนก่อนหน้า ให้ต่อยอดและพัฒนาเรื่องราวต่อไป**
- **ห้ามเด็ดขาด: ห้ามใส่คำสั่งเสียงประกอบ หรือ stage direction ใดๆ ทั้งสิ้น** เช่น ห้ามเขียน ""(เพลงประกอบ...)"" ""(เสียงเอฟเฟกต์...)"" ""(เพลงค่อยๆ ดังขึ้น...)"" ""(เพลงจางหายไป...)"" ""(เสียงบรรยากาศ...)"" เพราะบทนี้จะถูกส่งไป Text-to-Speech โดยตรง คำสั่งเหล่านี้จะถูกอ่านออกเสียงด้วย
- เขียนเฉพาะเนื้อหาที่ต้องการให้พูดออกเสียงเท่านั้น อนุญาตแค่ [Pause] เพื่อหยุดพัก

ตอบเป็นบทพูดเท่านั้น ไม่ต้องมีคำอธิบายอื่น";
    }

    // === Backward-compatible wrapper ===
    public static string GetSceneBasedScriptPrompt(string topic, int partNumber, string? previousPartsJson = null)
        => GetSceneBasedScriptPrompt(topic, partNumber, previousPartsJson, ContentCategoryRegistry.Animal);

    public static string GetSceneBasedScriptPrompt(string topic, int partNumber, string? previousPartsJson, ContentCategory category)
    {
        var (partDescription, transitionRule) = partNumber switch
        {
            1 => (
                "เกริ่นนำ - สร้างความสนใจ ตั้งคำถามที่ทำให้ผู้ฟังอยากรู้ต่อ บอกเล่าปูมหลังของเรื่อง",
                @"**กฎการเปิด-ปิดส่วนที่ 1:**
- เปิดด้วย: ""สวัสดีครับ ยินดีต้อนรับสู่ เรื่องแปลก น่ารู้..."" (แนะนำรายการและหัวข้อ)
- ห้ามปิดด้วย ""พบกันใหม่"" หรือ ""สวัสดีครับ"" เด็ดขาด
- ปิดด้วยคำเชื่อมเช่น: ""แต่นี่เป็นเพียงจุดเริ่มต้นเท่านั้น..."" หรือ ""เรื่องราวกำลังจะน่าสนใจยิ่งขึ้น..."""
            ),
            2 => (
                "เนื้อหาหลัก - ข้อมูลเชิงลึก หลักฐานทางวิทยาศาสตร์หรือประวัติศาสตร์ เรื่องราวที่น่าสนใจ",
                @"**กฎการเปิด-ปิดส่วนที่ 2:**
- ห้ามเปิดด้วย ""สวัสดี"" เด็ดขาด
- เปิดด้วยคำเชื่อมเช่น: ""จากที่เราได้เล่าไป..."" หรือเริ่มต้นด้วยเนื้อหาต่อเนื่องเลย
- ห้ามปิดด้วย ""พบกันใหม่"" หรือ ""สวัสดีครับ"" เด็ดขาด
- ปิดด้วยคำเชื่อมเช่น: ""แต่ยังมีอีกสิ่งหนึ่งที่ต้องพิจารณา..."""
            ),
            3 => (
                "สรุป - สรุปประเด็นสำคัญ ทิ้งคำถามให้คิดต่อ ปิดท้ายอย่างน่าจดจำ",
                @"**กฎการเปิด-ปิดส่วนที่ 3:**
- ห้ามเปิดด้วย ""สวัสดี"" เด็ดขาด
- เปิดด้วยคำเชื่อมเช่น: ""มาถึงตอนนี้..."" หรือเริ่มสรุปประเด็นเลย
- **ปิดท้ายด้วย:** ""หวังว่าเรื่องราววันนี้จะให้ความรู้และความบันเทิง แล้วพบกันใหม่ในตอนหน้า กับ เรื่องแปลก น่ารู้ สวัสดีครับ"""
            ),
            _ => ("เนื้อหา", "")
        };

        var previousContext = "";
        if (!string.IsNullOrWhiteSpace(previousPartsJson))
        {
            previousContext = $"\n\n=== บทที่เขียนไปแล้ว (ใช้เป็น context เพื่อให้เนื้อหาต่อเนื่อง ไม่ซ้ำ) ===\n{previousPartsJson}\n=== จบ context ===\n";
        }

        var structureHint = string.IsNullOrWhiteSpace(category.ScriptStructureHint)
            ? ""
            : $"\n- {category.ScriptStructureHint}";

        return $@"สร้างบทพูดสำหรับวิดีโอ YouTube podcast สารคดี ช่อง ""เรื่องแปลก น่ารู้""

หัวข้อ: {topic}
นี่คือส่วนที่ {partNumber} จาก 3 ส่วน (ทั้ง 3 ส่วนจะรวมเป็นวิดีโอเดียวกัน ความยาวรวมประมาณ 15-18 นาที)
ส่วนนี้คือ: {partDescription}

{transitionRule}
{previousContext}

**สำคัญมาก: ตอบเป็น JSON format ตามโครงสร้างนี้เท่านั้น:**
```json
{{
  ""scenes"": [
    {{
      ""text"": ""บทพูดภาษาไทยของ scene นี้..."",
      ""image_prompt"": ""English description of the visual scene for image generation""
    }},
    {{
      ""text"": ""บทพูดภาษาไทย scene ถัดไป..."",
      ""image_prompt"": ""English description of another visual scene""
    }}
  ]
}}
```

**กฎสำหรับ scenes:**
- แบ่งเป็น 16-24 scenes ต่อ Part (ยิ่งเยอะยิ่งดี เพื่อให้ภาพตรงกับเนื้อเรื่องทุกจังหวะ)
- แต่ละ scene มี text ประมาณ 50-70 คำ (สั้นพอที่ภาพ 1 ภาพจะสื่อได้ตรง)
- รวมทั้ง Part ประมาณ 1200 คำ (สำคัญมาก: TTS ภาษาไทยอ่านเร็วกว่าคนจริง ต้องเขียนให้ยาวเพียงพอ)
- **กฎสำคัญ: ห้าม scene ไหนมี text เกิน 100 คำ** ถ้าเนื้อหายาวให้แบ่งเป็น 2 scene
- แต่ละ scene ต้องมีจุดเปลี่ยนภาพชัดเจน เช่น เปลี่ยนฉาก เปลี่ยน subject เปลี่ยนมุมมอง หรือเปลี่ยนช่วงเวลา
- text {category.ScriptToneInstruction}
- มีจังหวะหยุดพัก [Pause] ในจุดที่เหมาะสม
- ห้ามใส่คำสั่งเสียงประกอบ stage direction ใดๆ ทั้งสิ้น อนุญาตแค่ [Pause]
- ปฏิบัติตามกฎการเปิด-ปิดอย่างเคร่งครัด
- ต้องไม่ซ้ำกับเนื้อหาส่วนก่อนหน้า{structureHint}

**กฎสำหรับ image_prompt (สำคัญมาก — ภาพต้องตรงกับเนื้อเรื่องที่เล่าในแต่ละ scene):**
- เขียนเป็นภาษาอังกฤษ 3-5 ประโยค ละเอียดที่สุดเท่าที่ทำได้
- image_prompt ต้อง ""แปลภาพจากเนื้อเรื่อง"" — สิ่งที่กำลังเล่าในขณะนั้นต้องปรากฏในภาพ
{category.ImagePromptSubjectGuidance}
- ระบุครบทุกองค์ประกอบ: (1) subject หลัก + ลักษณะเฉพาะ (2) action/pose ที่ตรงกับเนื้อเรื่อง (3) สภาพแวดล้อม/background (4) lighting/mood (5) camera angle/composition (6) รายละเอียดเสริม เช่น สี texture วัสดุ
- ตัวอย่าง: ""A massive tardigrade creature viewed under electron microscope, its eight stubby legs gripping tightly onto bright green moss. The translucent body glows with internal orange pigment. Surrounded by floating microscopic organisms, algae particles, and shimmering water droplets. Dramatic cyan side lighting creating deep purple shadows on the creature's segmented body, viewed from a low angle looking up through the water. Sharp macro detail showing the texture of the creature's cuticle skin.""
- **ความต่อเนื่องข้าม scene (สำคัญที่สุด):** ภาพทุก scene ต้องดูเป็นเรื่องเดียวกัน:
  * ใช้คำอธิบาย subject/ตัวละครหลักซ้ำเหมือนกันทุก scene (เช่น ""the same massive tardigrade with translucent body and orange pigment"")
  * ระบุลักษณะเด่นของ subject ที่ซ้ำกันทุก scene (สี ขนาด ลักษณะพิเศษ)
  * ใช้ lighting/mood โทนเดียวกันตลอด Part (เช่น ""warm golden lighting"" ทุก scene)
  * เปลี่ยนแค่ camera angle หรือ action ให้เล่าเรื่องต่อเนื่อง ไม่เปลี่ยน subject หรือ setting ทั้งหมด
- ห้ามใส่ style keywords (ระบบจะเติมให้เอง)
- ห้ามมีตัวอักษร/ข้อความในภาพ
- เน้น landscape composition (16:9)

ตอบเป็น JSON เท่านั้น ไม่ต้องมีคำอธิบายอื่นนอก JSON";
    }

    // === Backward-compatible wrapper ===
    public static string GetImagePromptGenerationPrompt(string topic)
        => GetImagePromptGenerationPrompt(topic, ContentCategoryRegistry.Animal);

    public static string GetImagePromptGenerationPrompt(string topic, ContentCategory category)
    {
        return $@"สร้าง prompt ภาษาอังกฤษสำหรับสร้างรูปปก YouTube video ในหัวข้อ: {topic}

รูปแบบที่ต้องการ:
- Style: {category.CoverImageStyleDescription}
- Technique: {category.CoverImageTechnique}
- Color Palette: {category.CoverImageColorPalette}
- Aspect Ratio: 16:9 (landscape)
- Composition:
  * ฉากกว้างที่แสดงองค์ประกอบหลักของเรื่อง
  * ต้องเหลือพื้นที่ว่างด้านบนหรือด้านข้างสำหรับใส่ข้อความ
  * ภาพต้องดึงดูดความสนใจ น่าคลิก
- ไม่ต้องมีตัวอักษรหรือข้อความในภาพ

ตอบเป็น prompt ภาษาอังกฤษเท่านั้น ประมาณ 2-3 ประโยค ไม่ต้องมีคำอธิบายอื่น";
    }

    // === Backward-compatible wrapper ===
    public static string GetMoodAnalysisPrompt(string scriptSummary)
        => GetMoodAnalysisPrompt(scriptSummary, ContentCategoryRegistry.Animal);

    public static string GetMoodAnalysisPrompt(string scriptSummary, ContentCategory category)
    {
        var moodList = string.Join(", ", category.MoodDescriptions.Keys);
        var moodDetails = string.Join("\n", category.MoodDescriptions.Select(kv => $"- {kv.Key}: {kv.Value}"));

        return $@"Analyze this Thai documentary script and classify its overall mood.
Choose exactly ONE mood from: {moodList}

{moodDetails}

Script excerpt:
{scriptSummary}

Reply with ONLY the mood word ({moodList}). Nothing else.";
    }
}

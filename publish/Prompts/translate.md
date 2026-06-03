### TASK: BIDIRECTIONAL TRANSLATION
Detect the language of the provided text and translate it with absolute contextual accuracy according to the routing rules below.

### LANGUAGE DETECTION & ROUTING RULES
- If <source_text> is strictly Non-Chinese: Translate it into **Simplified Chinese**.
- If <source_text> is strictly Chinese: Translate it into **English**.
- If <source_text> is a Mixed-Language input (containing both Chinese and English): Translate the entire text into **English**.

### INPUT DATA
<source_text>{SelectedText}</source_text>

### EXECUTION REQUIREMENTS
- Mirror the original formatting, paragraphs, and line breaks of <source_text> exactly.
- Output ONLY the translation.
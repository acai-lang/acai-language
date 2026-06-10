# 🍇 Acai Programming Language

**Acai** is a lightweight, human-friendly hybrid programming language with easy and different syntax styles.

---

## 🎨 Dual-Syntax Freedom (Human-Friendly)

Acai allows developers to write code using standard programming symbols **OR** plain, natural English words. The tokenizing engine processes both inputs seamlessly under the hood:

```text
# Style A: Using conversational words
make name = "XYZ"
if age GREATER THAN 18 then 
    show name 
end

# Style B: Using traditional operators
name = "XYZ"
if age > 18 then 
    show name 
end
```

---

## 🛠️ System Architecture (How it Works)

Acai utilizes a true hybrid pipeline split across two custom execution layers:

1. **The Compiler Engine:** Reads your source text scripts (`.acai`), parses your mixed keywords or symbols, and compresses them down into a compact stream of custom 1-byte Opcodes saved as binary asset files (`.cacai`).
2. **The Standalone Virtual Machine:** A custom, lightweight execution stack runtime built in C#. It bypasses the heavy .NET ecosystem completely, loading and running your bytecode stream line-by-line independently at fast speeds.

---

## 🚀 Basic Terminal Usage

Acai is portable, tiny, and executes scripts cleanly from any command console:

```bash
acai run demo/hello_world.acai
```

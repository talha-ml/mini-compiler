# 🚀 Modern Compiler Studio (Full Pipeline v4.0)

**Modern Compiler Studio** is a professional-grade, desktop-based compiler built from scratch using **C#**. It demonstrates a complete compilation journey, transforming high-level C-like source code into optimized **Three-Address Code (TAC)** and finally into **pseudo-ARM target assembly**.

Designed with a modern, dark-themed WinForms UI, this tool provides real-time visualization of every stage of the compiler frontend and backend.

---

## 📸 Project Showcase

### **Main Dashboard & Studio Interface**
<img width="100%" alt="Main Dashboard" src="https://github.com/user-attachments/assets/898735e2-0202-4bfa-a584-0556a327e4f4" />

### **Pipeline Stages & Error Handling**
| Abstract Syntax Tree (AST) Visualizer | Symbol Table & Semantic Logic |
| :---: | :---: |
| <img src="https://github.com/user-attachments/assets/2fab31ec-24d0-4bdc-b88d-b97f3ee575b1" width="100%" /> | <img src="https://github.com/user-attachments/assets/8cc9fc24-8f50-4812-9d1e-89430c9cc18d" width="100%" /> |

| IR Optimization & Assembly Generation | Syntax Validation & Error Popups |
| :---: | :---: |
| <img src="https://github.com/user-attachments/assets/abcebc18-cf47-4d8b-8269-024ad5fcbd1e" width="100%" /> | <img src="https://github.com/user-attachments/assets/1de16545-7a1c-4fe0-864a-72154a4f7751" width="100%" /> |

---

## 🛠 Key Specifications & Features

### 1. Lexical Analysis (Scanner)
- **Hand-written DFA:** No external tools used.
- **Robust Tokenization:** Keywords, identifiers, multi-line block comments, and floating-point literals.
- **Precision Tracking:** Error logging with exact Line and Column coordinates.

### 2. Syntax Analysis (Parser)
- **Recursive Descent:** Built-in grammar validation.
- **Operator Precedence:** Correctly handles arithmetic, relational, and logical precedence.
- **AST Construction:** Generates a hierarchical Abstract Syntax Tree.

### 3. Semantic Analysis
- **Scoped Symbol Table:** Manages global and local (nested) scopes using a stack-based dictionary.
- **Type Checking:** Detects type mismatches and undeclared variables/functions.
- **Redeclaration Protection:** Prevents duplicate identifiers within the same scope.

### 4. Intermediate Code & Optimization
- **TAC Generation:** Flattens complex logic into linear 3-Address Code.
- **Multi-pass Optimizer:** - **Constant Folding:** `10 + 20` → `30`.
  - **Copy Propagation:** Removes redundant variable assignments.
  - **Dead Code Elimination:** Strips unused temporary variables.

### 5. Target Code Generation
- **Architecture:** Maps logic to pseudo-ARM assembly.
- **Memory Management:** Handles Stack Frame Pointer (FP) and Stack Pointer (SP) offsets.
- **Register Allocation:** Dynamic mapping to CPU registers (R0-R7).

---

## 🎨 Modern UI & Visualizer
- **Custom GDI+ AST Renderer:** Visualizes the logic tree with color-coded nodes.
- **Interactive Code Editor:** Regex-based real-time syntax highlighting.
- **Live Execution Log:** Monitors the status and optimization reports of every phase.
- **Data Grids:** Tabular view of the Token stream and Symbol Table.

---

## ⌨️ Sample Code Support
```cpp
int factorial(int n) {
    if (n <= 1) {
        return 1;
    }
    return n * factorial(n - 1);
}

int main() {
    int i = 0;
    while (i < 5) {
        i++;
    }
    return 0;
}

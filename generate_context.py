import os
from datetime import datetime
import json
from typing import Dict, List, Optional, Tuple
import re
from collections import defaultdict

# Files and directories to ignore
IGNORE_PATTERNS = [
    '.env',
    '.git',
    '.idea',
    '.cursor',
    '__pycache__',
    '.vs',
    '.vscode',
    'node_modules',
    '.DS_Store',
    '*.pyc',
    '*.pyo',
    '*.pyd',
    '*.so',
    '*.dll',
    '*.dylib',
    '*.log',
    '*.pot',
    '*.sln',
    '*.user',
    '*.suo',
    '*.cache',
    'bin',
    'obj',
    'dist',
    'build',
    'ApiLogs',
    'generate_context.py',  # Ignore self
    'appsettings*.json',    # Ignore app settings
    'launchSettings.json'   # Ignore launch settings
]

# Constants
IGNORED_DIRS = ['.git', '__pycache__', 'node_modules', 'bin', 'obj', 'ApiLogs']
IGNORED_FILES = ['generate_context.py', '.gitignore', '.env', '.DS_Store', 'context_*.txt']

def should_ignore(path: str) -> bool:
    """Check if a path should be ignored."""
    name = os.path.basename(path)
    ignored_dirs = {'.git', '.vscode', '__pycache__', 'node_modules', 'bin', 'obj'}
    ignored_files = {'.gitignore', '.env', 'context_'}
    ignored_extensions = {'.pyc', '.pyo', '.pyd', '.dll', '.exe', '.log'}
    
    return (name in ignored_dirs or
            name in ignored_files or
            any(name.endswith(ext) for ext in ignored_extensions) or
            any(pattern in name for pattern in ignored_files))

def is_binary(file_path: str) -> bool:
    """Check if a file is binary."""
    try:
        with open(file_path, 'tr') as check_file:
            check_file.read(1024)
            return False
    except UnicodeDecodeError:
        return True

def extract_code_context(file_path: str) -> str:
    """Extract relevant code context from a source file."""
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    summary_parts = []
    
    # Extract imports based on file extension
    if file_path.endswith('.py'):
        imports = re.findall(r'^import .*|^from .* import .*', content, re.MULTILINE)
        if imports:
            summary_parts.append("Imports:\n" + "\n".join(imports))
    elif file_path.endswith('.cs'):
        imports = re.findall(r'^using .*?;', content, re.MULTILINE)
        if imports:
            summary_parts.append("Using Statements:\n" + "\n".join(imports))
    elif file_path.endswith(('.js', '.ts', '.tsx')):
        imports = re.findall(r'^import .*|^require\(.*\)', content, re.MULTILINE)
        if imports:
            summary_parts.append("Imports:\n" + "\n".join(imports))

    # For C# files, extract XML documentation, classes, interfaces, enums, and their members
    if file_path.endswith('.cs'):
        # Extract namespace
        namespace_match = re.search(r'namespace ([\w.]+);', content)
        if namespace_match:
            summary_parts.append(f"\nNamespace: {namespace_match.group(1)}")

        # Find all XML documentation comments
        xml_docs = {}
        for match in re.finditer(r'(?ms)/// <summary>\s*///\s*([^<]*?)///\s*</summary>.*?(?=\s*(?:public|private|protected|internal|class|interface|enum)\s+\w+)', content):
            doc = match.group(1).strip().replace('/// ', '')
            start_pos = match.end()
            # Find the next type or member declaration
            next_decl = re.search(r'(?:public|private|protected|internal)\s+(?:class|interface|enum|[\w<>]+\s+\w+)', content[start_pos:])
            if next_decl:
                xml_docs[start_pos + next_decl.start()] = doc

        # Extract classes, interfaces, and enums with their members
        type_matches = re.finditer(r'(?ms)(?:public|private|protected|internal)\s+(class|interface|enum)\s+(\w+)(?:\s*:\s*([\w,\s]+))?\s*\{(.*?)\}(?=\s*(?:public|private|protected|internal|class|interface|enum|\s*$))', content)
        
        for match in type_matches:
            type_kind = match.group(1)
            type_name = match.group(2)
            base_types = match.group(3)
            body = match.group(4)

            type_info = [f"\n{type_kind.title()}: {type_name}"]
            if base_types:
                type_info.append(f"Inherits/Implements: {base_types.strip()}")

            # Add documentation if available
            doc_pos = match.start()
            if doc_pos in xml_docs:
                type_info.append(f"Documentation: {xml_docs[doc_pos]}")

            if type_kind == 'enum':
                # Extract enum values
                enum_values = re.findall(r'(\w+)(?:\s*=\s*([^,\s}]+))?', body)
                if enum_values:
                    values_str = "\n  ".join(f"{v[0]}" + (f" = {v[1]}" if v[1] else "") for v in enum_values)
                    type_info.append(f"Values:\n  {values_str}")
            else:
                # Extract properties
                properties = re.finditer(r'(?ms)(?:public|private|protected|internal)\s+(?:virtual\s+)?([^\n{]+?)\s+(\w+)\s*(?:\{[^}]*\}|;)', body)
                props = []
                for prop in properties:
                    prop_type = prop.group(1).strip()
                    prop_name = prop.group(2)
                    props.append(f"{prop_name}: {prop_type}")
                
                if props:
                    type_info.append("Properties:\n  " + "\n  ".join(props))

                # Extract methods
                methods = re.finditer(r'(?ms)(?:public|private|protected|internal)\s+(?:virtual\s+)?([^\n{]+?)\s+(\w+)\s*\([^)]*\)\s*(?:\{[^}]*\}|;)', body)
                meths = []
                for meth in methods:
                    return_type = meth.group(1).strip()
                    method_name = meth.group(2)
                    if not method_name.startswith('get_') and not method_name.startswith('set_'):
                        meths.append(f"{method_name}(): {return_type}")
                
                if meths:
                    type_info.append("Methods:\n  " + "\n  ".join(meths))

            summary_parts.append("\n".join(type_info))

    # For Python files, extract classes and functions
    elif file_path.endswith('.py'):
        # Extract classes with their methods
        classes = re.finditer(r'(?ms)^class\s+(\w+)(?:\([^)]+\))?\s*:\s*(.*?)(?=^(?:class|def|\Z))', content, re.MULTILINE)
        for match in classes:
            class_name = match.group(1)
            class_body = match.group(2)
            
            class_info = [f"\nClass: {class_name}"]
            
            # Extract docstring
            docstring_match = re.match(r'\s*"""(.*?)"""', class_body, re.DOTALL)
            if docstring_match:
                class_info.append(f"Documentation: {docstring_match.group(1).strip()}")
            
            # Extract methods
            methods = re.finditer(r'def\s+(\w+)\s*\([^)]*\):', class_body)
            meths = []
            for meth in methods:
                meths.append(meth.group(1))
            
            if meths:
                class_info.append("Methods:\n  " + "\n  ".join(meths))
            
            summary_parts.append("\n".join(class_info))

        # Extract standalone functions
        functions = re.finditer(r'(?ms)^def\s+(\w+)\s*\([^)]*\):\s*(.*?)(?=^(?:def|class|\Z))', content, re.MULTILINE)
        funcs = []
        for match in functions:
            func_name = match.group(1)
            func_body = match.group(2)
            
            # Extract docstring
            docstring_match = re.match(r'\s*"""(.*?)"""', func_body, re.DOTALL)
            if docstring_match:
                funcs.append(f"{func_name}: {docstring_match.group(1).strip()}")
            else:
                funcs.append(func_name)
        
        if funcs:
            summary_parts.append("\nFunctions:\n  " + "\n  ".join(funcs))

    # For JavaScript/TypeScript files
    elif file_path.endswith(('.js', '.ts', '.tsx')):
        # Extract classes
        classes = re.finditer(r'(?ms)^class\s+(\w+)(?:\s+extends\s+\w+)?\s*\{(.*?)\}', content, re.MULTILINE)
        for match in classes:
            class_name = match.group(1)
            class_body = match.group(2)
            
            class_info = [f"\nClass: {class_name}"]
            
            # Extract methods
            methods = re.finditer(r'(?:async\s+)?(\w+)\s*\([^)]*\)\s*\{', class_body)
            meths = []
            for meth in methods:
                meths.append(meth.group(1))
            
            if meths:
                class_info.append("Methods:\n  " + "\n  ".join(meths))
            
            summary_parts.append("\n".join(class_info))

        # Extract functions
        functions = re.finditer(r'(?:export\s+)?(?:async\s+)?function\s+(\w+)\s*\([^)]*\)', content)
        funcs = []
        for match in functions:
            funcs.append(match.group(1))
        
        if funcs:
            summary_parts.append("\nFunctions:\n  " + "\n  ".join(funcs))

    return "\n".join(summary_parts) if summary_parts else "No code context extracted."

def find_block_end(content: str, start: int) -> int:
    """Find the end of a code block starting at the given position."""
    # Find the start of the block
    block_start = content.find(':', start)
    if block_start == -1:
        block_start = content.find('{', start)
    if block_start == -1:
        return start
    
    # Handle Python-style blocks (indentation)
    if content[block_start] == ':':
        block_start = content.find('\n', block_start) + 1
        if block_start == 0:
            return start
            
        # Get the indentation of the first line
        match = re.match(r'[ \t]*', content[block_start:])
        if not match:
            return start
        base_indent = match.group(0)
        
        # Find the first line with same or less indentation
        pos = block_start
        while pos < len(content):
            line_start = pos
            line_end = content.find('\n', pos)
            if line_end == -1:
                break
                
            line = content[line_start:line_end]
            if line.strip() and not line.startswith(base_indent):
                return line_start
                
            pos = line_end + 1
        return len(content)
    
    # Handle C-style blocks (braces)
    else:
        stack = ['{']
        pos = block_start + 1
        
        while pos < len(content) and stack:
            char = content[pos]
            if char == '{':
                stack.append(char)
            elif char == '}':
                stack.pop()
            pos += 1
            
        return pos

def get_file_summary(file_path: str, content: str) -> Dict:
    """Get a summary of a file's contents."""
    summary = {
        "type": get_file_type(file_path),
        "lines": len(content.splitlines())
    }
    
    # Add word count for documentation files
    if summary["type"] == "documentation":
        summary["words"] = len(content.split())
    
    # Extract code context for source files
    if summary["type"] == "source_code":
        summary["code_context"] = extract_code_context(file_path)
    
    return summary

def generate_file_tree(root_dir: str) -> Dict:
    """Generate a tree structure of the repository."""
    tree = {"files": [], "dirs": {}}
    
    for root, dirs, files in os.walk(root_dir):
        # Skip ignored directories
        dirs[:] = [d for d in dirs if not should_ignore(os.path.join(root, d))]
        
        # Get relative path parts
        rel_path = os.path.relpath(root, root_dir)
        if rel_path == '.':
            # Add files to root
            for file in files:
                if not should_ignore(os.path.join(root, file)):
                    tree["files"].append(file)
            continue
            
        # Navigate to the correct spot in the tree
        current = tree
        path_parts = rel_path.split(os.sep)
        
        for part in path_parts:
            if part not in current["dirs"]:
                current["dirs"][part] = {"files": [], "dirs": {}}
            current = current["dirs"][part]
        
        # Add files to current directory
        for file in files:
            if not should_ignore(os.path.join(root, file)):
                current["files"].append(file)
    
    return tree

def write_tree(f, tree, prefix=''):
    """Write the tree structure to the file."""
    items = sorted(tree.items())
    for i, (name, value) in enumerate(items):
        is_last = i == len(items) - 1
        
        # Determine the branch symbol
        branch = '└── ' if is_last else '├── '
        
        # Write the current item
        f.write(f"{prefix}{branch}{name}")
        if isinstance(value, (int, float)):
            f.write(f" ({format_size(value)})")
        f.write('\n')
        
        # Recursively write children
        if isinstance(value, dict):
            new_prefix = prefix + ('    ' if is_last else '│   ')
            write_tree(f, value, new_prefix)

def is_text_file(file_path: str) -> bool:
    """Check if a file is a text file based on its extension."""
    text_extensions = {
        '.txt', '.md', '.py', '.cs', '.js', '.ts', '.tsx', '.jsx',
        '.json', '.yaml', '.yml', '.xml', '.ini', '.config',
        '.gitignore', '.env', '.rst', '.log', '.css', '.scss',
        '.html', '.htm', '.csproj', '.sln', '.sh', '.bat', '.ps1'
    }
    return os.path.splitext(file_path)[1].lower() in text_extensions

def generate_context_document():
    """Generate a context document summarizing the repository."""
    # Initialize statistics
    total_files = 0
    total_size = 0
    file_types = defaultdict(int)
    file_summaries = []
    
    # Process files
    for root, dirs, files in os.walk('.'):
        # Skip ignored directories
        dirs[:] = [d for d in dirs if not should_ignore(d)]
        
        for file in files:
            if should_ignore(file):
                continue
                
            file_path = os.path.join(root, file)
            
            try:
                # Get file size
                size = os.path.getsize(file_path)
                total_size += size
                total_files += 1
                
                # Determine file type
                file_type = get_file_type(file_path)
                file_types[file_type] += 1
                
                # Extract code context for source files
                context = ""
                if file_type == "source_code":
                    context = extract_code_context(file_path)
                
                # Generate file summary
                summary = {
                    'path': file_path,
                    'type': file_type,
                    'size': size,
                    'context': context if context else None
                }
                file_summaries.append(summary)
                
            except Exception as e:
                print(f"Error processing {file_path}: {str(e)}")
    
    # Generate timestamp for the output file
    timestamp = datetime.utcnow().strftime('%Y%m%d_%H%M%S')
    output_file = f'context_{timestamp}.txt'
    
    # Write context document
    with open(output_file, 'w', encoding='utf-8') as f:
        # Write repository overview
        f.write("Repository Overview\n")
        f.write("==================\n\n")
        f.write(f"Total Files: {total_files}\n")
        f.write(f"Total Size: {format_size(total_size)}\n\n")
        
        # Write file type distribution
        f.write("File Types\n")
        f.write("---------\n")
        for file_type, count in sorted(file_types.items()):
            f.write(f"{file_type}: {count} files\n")
        f.write("\n")
        
        # Write file structure
        f.write("File Structure\n")
        f.write("-------------\n")
        tree = {}
        for summary in file_summaries:
            add_to_tree(tree, summary['path'].split(os.sep)[1:], summary['size'])
        write_tree(f, tree)
        f.write("\n")
        
        # Write file summaries
        f.write("File Summaries\n")
        f.write("--------------\n")
        for summary in sorted(file_summaries, key=lambda x: x['path']):
            f.write(f"\n{summary['path']}\n")
            f.write("=" * len(summary['path']) + "\n")
            f.write(f"Type: {summary['type']}\n")
            f.write(f"Size: {format_size(summary['size'])}\n")
            if summary.get('context'):
                f.write("\nCode Context:\n")
                f.write(summary['context'])
            f.write("\n\n")
    
    print(f"Context document generated: {output_file}")
    return output_file

def add_to_tree(tree, parts, size):
    """Add a file to the tree structure."""
    if not parts:
        return
    
    current = parts[0]
    remaining = parts[1:]
    
    if remaining:
        if current not in tree:
            tree[current] = {}
        add_to_tree(tree[current], remaining, size)
    else:
        if current not in tree:
            tree[current] = size

def format_size(size: int) -> str:
    """Format file size in human readable format."""
    for unit in ['B', 'KB', 'MB', 'GB']:
        if size < 1024:
            return f"{size:.2f} {unit}"
        size /= 1024
    return f"{size:.2f} TB"

def get_file_type(file_path: str) -> str:
    """Determine the type of a file based on its extension and content."""
    ext = os.path.splitext(file_path)[1].lower()
    
    # Source code files
    if ext in ['.py', '.cs', '.js', '.ts', '.tsx', '.jsx', '.java', '.cpp', '.h', '.hpp']:
        return "source_code"
    
    # Documentation files
    if ext in ['.md', '.txt', '.rst', '.doc', '.docx', '.pdf']:
        return "documentation"
    
    # Configuration files
    if ext in ['.json', '.yaml', '.yml', '.xml', '.ini', '.config', '.env']:
        return "configuration"
    
    # Project files
    if ext in ['.csproj', '.sln', '.pyproj', '.npmrc', '.gitignore']:
        return "project"
    
    # Log files
    if ext in ['.log']:
        return "log"
    
    # Test files
    if 'test' in file_path.lower() or 'spec' in file_path.lower():
        return "test"
    
    return "other"

if __name__ == "__main__":
    generate_context_document() 
#!/usr/bin/env python3
"""Convert markdown to PDF using reportlab."""

import sys
from markdown2 import markdown
from reportlab.lib.pagesizes import letter, A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak, Table, TableStyle
from reportlab.lib.enums import TA_LEFT, TA_CENTER, TA_JUSTIFY
from reportlab.lib import colors
import re

def markdown_to_pdf(markdown_file, pdf_file):
    """Convert markdown file to PDF."""
    
    # Read markdown
    with open(markdown_file, 'r', encoding='utf-8') as f:
        md_content = f.read()
    
    # Convert to HTML (intermediate step)
    html_content = markdown(md_content, extras=['tables', 'code-friendly', 'fenced-code-blocks'])
    
    # Create PDF
    doc = SimpleDocTemplate(pdf_file, pagesize=letter,
                          rightMargin=0.75*inch,
                          leftMargin=0.75*inch,
                          topMargin=0.75*inch,
                          bottomMargin=0.75*inch,
                          title="TRE Agent Provenance Implementation")
    
    story = []
    styles = getSampleStyleSheet()
    
    # Custom styles
    title_style = ParagraphStyle(
        'CustomTitle',
        parent=styles['Heading1'],
        fontSize=24,
        textColor=colors.HexColor('#1f4788'),
        spaceAfter=12,
        alignment=TA_CENTER,
        fontName='Helvetica-Bold'
    )
    
    heading1_style = ParagraphStyle(
        'CustomHeading1',
        parent=styles['Heading1'],
        fontSize=16,
        textColor=colors.HexColor('#1f4788'),
        spaceAfter=10,
        spaceBefore=10,
        fontName='Helvetica-Bold'
    )
    
    heading2_style = ParagraphStyle(
        'CustomHeading2',
        parent=styles['Heading2'],
        fontSize=13,
        textColor=colors.HexColor('#2d5aa0'),
        spaceAfter=8,
        spaceBefore=8,
        fontName='Helvetica-Bold'
    )
    
    body_style = ParagraphStyle(
        'CustomBody',
        parent=styles['BodyText'],
        fontSize=10,
        alignment=TA_JUSTIFY,
        spaceAfter=6
    )
    
    code_style = ParagraphStyle(
        'CustomCode',
        parent=styles['Normal'],
        fontSize=8,
        fontName='Courier',
        textColor=colors.HexColor('#333333'),
        leftIndent=12
    )
    
    # Parse and add content
    lines = md_content.split('\n')
    i = 0
    while i < len(lines):
        line = lines[i]
        
        # Skip empty lines
        if not line.strip():
            story.append(Spacer(1, 0.1*inch))
            i += 1
            continue
        
        # Title
        if line.startswith('# ') and '##' not in line:
            text = line[2:].strip()
            story.append(Paragraph(text, title_style))
            story.append(Spacer(1, 0.15*inch))
            i += 1
            continue
        
        # Heading 1
        if line.startswith('## '):
            text = line[3:].strip()
            story.append(Paragraph(text, heading1_style))
            i += 1
            continue
        
        # Heading 2
        if line.startswith('### '):
            text = line[4:].strip()
            story.append(Paragraph(text, heading2_style))
            i += 1
            continue
        
        # Code blocks
        if line.startswith('```'):
            code_lines = []
            i += 1
            while i < len(lines) and not lines[i].startswith('```'):
                code_lines.append(lines[i])
                i += 1
            code_text = '\n'.join(code_lines).strip()
            # Escape HTML entities
            code_text = code_text.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')
            story.append(Paragraph(f'<font face="Courier" size="8">{code_text}</font>', code_style))
            story.append(Spacer(1, 0.1*inch))
            i += 1
            continue
        
        # Regular paragraph
        # Clean markdown formatting
        text = line.strip()
        text = re.sub(r'\*\*(.*?)\*\*', r'<b>\1</b>', text)
        text = re.sub(r'\*(.*?)\*', r'<i>\1</i>', text)
        text = re.sub(r'`(.*?)`', r'<font face="Courier"><b>\1</b></font>', text)
        text = re.sub(r'\[(.*?)\]\((.*?)\)', r'<u>\1</u>', text)
        
        if text:
            story.append(Paragraph(text, body_style))
        
        i += 1
    
    # Add page break before final section
    if len(story) > 50:
        story.insert(len(story) - 10, PageBreak())
    
    # Build PDF
    doc.build(story)
    print(f"✓ PDF created: {pdf_file}")

if __name__ == '__main__':
    markdown_file = 'PROVENANCE_IMPLEMENTATION.md'
    pdf_file = 'PROVENANCE_IMPLEMENTATION.pdf'
    
    try:
        markdown_to_pdf(markdown_file, pdf_file)
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

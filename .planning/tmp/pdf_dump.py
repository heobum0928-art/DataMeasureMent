# -*- coding: utf-8 -*-
import fitz, io, sys

PDF = r'C:\Info\Doc\2.디팜스테크\02_설계\SOP\260303_Rapicity_A8.1_Z-Stopper_MSOP_RevB_변경내역 표기.pdf'
OUT = r'C:\Info\Project\DataMeasurement\.planning\tmp\pdf_text.txt'

doc = fitz.open(PDF)
with io.open(OUT, 'w', encoding='utf-8') as f:
    f.write('PAGES: %d\n\n' % doc.page_count)
    for i in range(doc.page_count):
        t = doc[i].get_text()
        f.write('===== PAGE %d (%d chars) =====\n' % (i + 1, len(t)))
        f.write(t)
        f.write('\n\n')
print('OK pages=%d' % doc.page_count)

# -*- coding: utf-8 -*-
import fitz

PDF = r'C:\Info\Doc\2.디팜스테크\02_설계\SOP\260303_Rapicity_A8.1_Z-Stopper_MSOP_RevB_변경내역 표기.pdf'
OUTDIR = r'C:\Info\Project\DataMeasurement\.planning\tmp'

doc = fitz.open(PDF)
# pages are 1-based in the doc; fitz index is 0-based
for pno in [12, 20, 21, 22, 23]:
    page = doc[pno - 1]
    pix = page.get_pixmap(dpi=200)
    out = OUTDIR + ('\\pdf_p%02d.png' % pno)
    pix.save(out)
    print('saved %s (%dx%d)' % (out, pix.width, pix.height))

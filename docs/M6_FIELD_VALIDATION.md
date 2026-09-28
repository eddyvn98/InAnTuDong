# M6 Field Validation Matrix

Status: pending real-machine validation.

This file records only physical/provider validation that cannot be proven by CI. Do not mark an item PASS from automated tests alone.

## Rules

- Record the exact machine/printer/provider used.
- Record date, app commit/version and relevant driver/software version.
- Use PASS / FAIL / BLOCKED.
- Attach measurements or a short observation when physical output matters.
- Never mark a printer/profile physically verified by model name alone.

## 1. Manual duplex on target simplex printer

Goal: prove that duplex intent does not silently become simplex and that the saved reinsert profile matches real paper transport.

Test cases:

- 2 pages, LongEdge
- 2 pages, ShortEdge
- 5 pages, LongEdge
- 6 pages, LongEdge
- 4 pages x 2 copies
- close/reopen app while waiting for reinsert, then continue back pass
- force/cause a back-pass failure where practical, then retry backs without reprinting fronts
- attempt to change printer between front and back pass; app must block continuation
- verify front/back orientation and page pairing
- save printer-specific back order, rotation and reinsert instruction only after physical confirmation

Record:

| Case | Result | Observation / measurement |
| --- | --- | --- |
| 2p LongEdge | PENDING | |
| 2p ShortEdge | PENDING | |
| 5p LongEdge | PENDING | |
| 6p LongEdge | PENDING | |
| 4p x2 copies | PENDING | |
| Restart before back pass | PENDING | |
| Back-pass retry | PENDING | |
| Printer mismatch guard | PENDING | |

## 2. 4x6 paper / driver

Goal: prove that the Windows driver advertises and receives the requested 101.6 x 152.4 mm stock without silent A4 scaling.

- select a real 4x6-capable printer/driver
- generate 1-6 item auto-layout candidates
- print the selected candidate
- measure physical width/height and obvious margins/crop
- confirm unsupported stock fails clearly instead of silently scaling

| Case | Result | Observation / measurement |
| --- | --- | --- |
| Driver advertises 4x6 | PENDING | |
| Physical 4x6 output | PENDING | |
| Unsupported stock guard | PENDING | |

## 3. Smart Collage AI live provider

Goal: validate the multimodal provider path, not only deterministic fallback.

- configure a real vision-capable OpenAI-compatible endpoint/model
- use representative three-photo sets
- confirm all three images are considered
- confirm returned template/source assignments stay within the allowlist
- inspect crop/zoom/pan quality in preview
- disconnect or force provider failure and confirm deterministic fallback still works

| Case | Result | Observation |
| --- | --- | --- |
| Live multimodal request | PENDING | |
| Valid bounded proposal | PENDING | |
| Preview quality | PENDING | |
| Provider failure fallback | PENDING | |

## 4. HEIC / HEIF

- open a representative iPhone HEIC/HEIF image in the packaged Windows app
- inspect metadata and preview
- print/preview through the normal pipeline
- confirm no missing native/runtime dependency

| Case | Result | Observation |
| --- | --- | --- |
| Representative iPhone HEIC | PENDING | |

## 5. Office / LibreOffice

Use representative DOCX, XLSX and PPTX files with an installed LibreOffice version recorded below.

For each file:

- convert to PDF
- compare page count/content against the source
- inspect fonts, tables/images and page breaks
- preview and print through the normal pipeline

Excel additionally:

- exercise Excel Smart Print
- confirm original workbook is unchanged
- confirm wide data is not shrunk below the readability guardrail
- confirm repeated headers / leading columns where applicable

| Format | Result | Observation |
| --- | --- | --- |
| DOCX | PENDING | |
| XLSX normal conversion | PENDING | |
| XLSX Excel Smart Print | PENDING | |
| PPTX | PENDING | |

## 6. Epson L3310 WIA scan

- enumerate scanner
- scan representative page
- exercise crop/deskew
- create scan PDF
- use scanned result as a print source
- verify failure/no-device messaging where applicable

| Case | Result | Observation |
| --- | --- | --- |
| WIA enumeration | PENDING | |
| Image scan | PENDING | |
| Crop/deskew | PENDING | |
| Scan PDF | PENDING | |
| Scan -> preview/print | PENDING | |

## Release decision

Tagged preview release is allowed only after:

- automated CI/package gates remain green
- manual duplex is physically verified on the intended target simplex printer
- release-relevant failures discovered above are either fixed or explicitly documented as non-blocking limitations
- no test result is overstated as verified when it was not exercised on real hardware/provider data

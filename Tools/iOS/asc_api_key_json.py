#!/usr/bin/env python3
"""Writes fastlane's App Store Connect API key JSON from the TestFlight job's secrets.

    python3 asc_api_key_json.py <out.json>     # reads ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_P8
    python3 asc_api_key_json.py --self-test

Used by .github/workflows/ios-testflight.yml (Docs/IOS_BUILD.md section 2, Path C).

ASC_KEY_P8 is pasted by hand into GitHub, so it arrives in several shapes. All of them are
accepted and turned back into the .p8 file's PEM text:

  - the .p8 file's text, BEGIN and END lines included (what Apple's download contains);
  - the same text with its newlines escaped as a literal backslash-n;
  - base64 of the whole .p8 file;
  - only the key body, the base64 between the BEGIN and END lines. That body decodes to the
    key's DER bytes, which start with 0x30 (an ASN.1 SEQUENCE). Run 3 of the workflow died on
    exactly this shape: decoding the body as text failed on the 0x81 that follows the 0x30.

Nothing secret is ever printed. A shape that matches none of these fails with a message naming
the shape that was expected.
"""
import base64
import binascii
import json
import os
import re
import sys

PEM_HEADER = "-----BEGIN PRIVATE KEY-----"
PEM_FOOTER = "-----END PRIVATE KEY-----"
EXPECTED = ("Paste the whole AuthKey_XXXXXXXXXX.p8 file into the ASC_KEY_P8 secret, including its "
            "-----BEGIN PRIVATE KEY----- and -----END PRIVATE KEY----- lines (Docs/IOS_BUILD.md Path C).")


class KeyShapeError(ValueError):
    pass


def wrap_der_body(body):
    lines = [body[i:i + 64] for i in range(0, len(body), 64)]
    return PEM_HEADER + "\n" + "\n".join(lines) + "\n" + PEM_FOOTER + "\n"


def normalize_p8(raw):
    """Returns (pem_text, shape) for any accepted shape of the secret; raises KeyShapeError."""
    text = raw.replace("\r", "").strip()
    if "\\n" in text and "\n" not in text:
        text = text.replace("\\n", "\n")
    if "-----BEGIN" in text:
        return text + "\n", "pem"

    body = "".join(text.split())
    if not body:
        raise KeyShapeError("ASC_KEY_P8 is empty. " + EXPECTED)
    try:
        data = base64.b64decode(body, validate=True)
    except (binascii.Error, ValueError):
        raise KeyShapeError("ASC_KEY_P8 is neither the .p8 file's text nor base64. " + EXPECTED)

    try:
        decoded = data.decode("ascii")
    except UnicodeDecodeError:
        decoded = None
    if decoded is not None and "-----BEGIN" in decoded:
        return decoded.replace("\r", "").strip() + "\n", "base64 of the .p8 file"
    if data[:1] == b"\x30":
        return wrap_der_body(body), "key body without its BEGIN/END lines"
    raise KeyShapeError("ASC_KEY_P8 decodes to something that is not a private key. " + EXPECTED)


def main(out_path):
    try:
        pem, shape = normalize_p8(os.environ.get("ASC_KEY_P8", ""))
    except KeyShapeError as e:
        print(f"::error::{e}")
        return 1
    if shape != "pem":
        print(f"ASC_KEY_P8 holds the {shape}; rebuilt the .p8 text from it.")

    key_id = os.environ.get("ASC_KEY_ID", "").strip()
    issuer_id = os.environ.get("ASC_ISSUER_ID", "").strip()
    # Warnings only: a format change on Apple's side must not block an upload.
    if not re.fullmatch(r"[A-Z0-9]{10}", key_id):
        print("::warning::ASC_KEY_ID does not look like a 10-character Key ID "
              "(App Store Connect > Users and Access > Integrations).")
    if not re.fullmatch(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
                        issuer_id):
        print("::warning::ASC_ISSUER_ID does not look like the Issuer ID, a UUID shown above the keys list.")

    fd = os.open(out_path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(fd, "w") as f:
        json.dump({"key_id": key_id, "issuer_id": issuer_id, "key": pem, "in_house": False}, f)
    return 0


def self_test():
    # A made-up DER blob with the shape of an EC P-256 PKCS#8 key (30 81 93 ...). It is not a key.
    der = b"\x30\x81\x93\x02\x01\x00" + bytes(range(141))
    body = base64.b64encode(der).decode()
    pem = wrap_der_body(body)
    cases = {
        "pem": pem,
        "pem with CRLF": pem.replace("\n", "\r\n"),
        "pem with escaped newlines": pem.strip().replace("\n", "\\n"),
        "base64 of the file": base64.b64encode(pem.encode()).decode(),
        "body only": body,
        "body only, wrapped": "\n".join(body[i:i + 64] for i in range(0, len(body), 64)),
    }
    failures = []
    for name, value in cases.items():
        got, _ = normalize_p8(value)
        if got != pem:
            failures.append(name)
    for name, value in {"empty": "  ", "not base64": "this is not a key!",
                        "base64 of other text": base64.b64encode(b"hello world").decode()}.items():
        try:
            normalize_p8(value)
            failures.append(name + " (accepted)")
        except KeyShapeError:
            pass
    if failures:
        print("self-test FAILED: " + ", ".join(failures))
        return 1
    print(f"self-test passed: {len(cases)} accepted shapes, 3 rejected shapes.")
    return 0


if __name__ == "__main__":
    if sys.argv[1:] == ["--self-test"]:
        sys.exit(self_test())
    if len(sys.argv) != 2:
        sys.exit("usage: asc_api_key_json.py <out.json> | --self-test")
    sys.exit(main(sys.argv[1]))

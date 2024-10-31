import json
import os
import time
import uuid
import boto3
from botocore.exceptions import ClientError


def lambda_handler(event, context):
    print("Received event: " + json.dumps(event, indent=2))

    # receive GET event from CloudFront if not return unsupported method
    if event["requestContext"]["http"]["method"] != "GET":
        return {
            "statusCode": 405,
            "body": json.dumps({"message": "Unsupported method"}),
        }

    # get bucket name and object name from environment variables
    s3_client = boto3.client("s3")
    bucket_name = os.environ["BUCKET_NAME"]
    folder_name = get_folder_key()

    try:
        conditions = [
            # ["starts-with", "$Content-Type", "image/"],
            ["starts-with", "$key", folder_name],
            ["content-length-range", 0, 52428800],  # 50MB
        ]
        response = s3_client.generate_presigned_post(
            Bucket=bucket_name,
            Key=folder_name + "${filename}",
            Fields=None,
            Conditions=conditions,
            ExpiresIn=900,  # 15 minutes
        )
    except ClientError as e:
        print(e)
        return {
            "statusCode": 500,
            "body": json.dumps({"message": "Error generating presigned URL"}),
        }

    return {
        "statusCode": 200,
        "body": json.dumps(
            {
                "message": "Presigned URL generated",
                "url": response["url"],
                "fields": response["fields"],
            }
        ),
    }


def get_folder_key() -> str:
    return time.strftime("%Y%m%d/") + uuid.uuid4().hex + "/"

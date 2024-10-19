import json
import os
import boto3
from botocore.exceptions import ClientError


def lambda_handler(event, context):
    print("Received event: " + json.dumps(event, indent=2))
    print("Context: " + json.dumps(context, indent=2))

    # receive GET event from CloudFront if not return unsupported method
    if event["httpMethod"] != "GET":
        return {
            "statusCode": 405,
            "body": json.dumps({"message": "Unsupported method"}),
        }

    # get bucket name and object name from environment variables
    s3_client = boto3.client("s3")
    bucket_name = os.environ["BUCKET_NAME"]
    object_name = "your-object-name"

    try:
        conditions = [
            ["starts-with", "$Content-Type", "image/"],
            ["content-length-range", 0, 52428800],  # 50MB
        ]
        response = s3_client.generate_presigned_post(
            Bucket=bucket_name,
            Key=object_name,
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

    return {
        "statusCode": 200,
        "body": json.dumps(
            {
                "message": "hello world",
                # "location": ip.text.replace("\n", "")
            }
        ),
    }

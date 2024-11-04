import json
import os
import time
import uuid
import boto3


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
    result_folder = os.environ["RESULT_FOLDER_PATH"]
    allowed_file_size = int(os.environ["ALLOWED_FILE_SIZE"])
    folder_name = get_folder_key(result_folder)

    try:
        conditions = [
            ["starts-with", "$Content-Type", "image/"],
            ["starts-with", "$key", folder_name],
            ["content-length-range", 1, allowed_file_size],
        ]

        response = s3_client.generate_presigned_post(
            Bucket=bucket_name,
            Key=folder_name + "${filename}",
            Fields=None,
            Conditions=conditions,
            ExpiresIn=300,  # 5 mins
        )
    except Exception as e:
        print(e)
        return {
            "statusCode": 500,
            "body": json.dumps({"message": "Internal server error"}),
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


def get_folder_key(result_folder: str) -> str:
    return result_folder + time.strftime("%Y%m%d/") + uuid.uuid4().hex + "/"

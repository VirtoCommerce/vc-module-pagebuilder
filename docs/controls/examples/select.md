
``` json
{
  "id": "author",
  "label": "Add all authors",
  "type": "select",
  "tab":"Content",
  "displayField": "name",
  "equalKey": "id",
  "multiple":true,
  "request": {
    "url": "/api/members/search",
    "method": "post",
    "label": "name",
    "response": {
      "result": "results",
      "isArray": true,
      "value": [
        "id",
        "firstName",
        "name",
        "lastName",
        "photoUrl",
        {
          "key": "jobTitle",
          "query": "$..dynamicProperties[?(@.name=='jobTitle')].values..value",
          "isArray": false
        },
        {
          "key": "biography",
          "query": "$..dynamicProperties[?(@.name=='biography')].values..value",
          "isArray": false
        },
        {
          "key": "linkedinLink",
          "query": "$..dynamicProperties[?(@.name=='linkedinLink')].values..value",
          "isArray": false
        },
        {
          "key": "githubLink",
          "query": "$..dynamicProperties[?(@.name=='githubLink')].values..value",
          "isArray": false
        }
      ]
    },
    "body": {
      "memberId": null,
      "deepSearch": true,
      "responseGroup": "Full",
      "sort": "memberType:desc;name:asc",
      "searchPhrase": "isAuthor:true"
    }
  },
  "options": [
      {
        "value":null,
        "label": "[null]"
      }
    ]
}
```
